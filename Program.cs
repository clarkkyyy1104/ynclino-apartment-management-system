using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Filters;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Per-developer overrides (your local MySQL password) live in appsettings.Local.json,
// which is git-ignored so secrets never get committed.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllersWithViews(options =>
{
    // force users flagged for a password reset onto the change page until they comply
    options.Filters.Add<MustChangePasswordFilter>();
});

// The connection string (including the DATABASE NAME) comes from the committed
// appsettings.json, so each branch can target its own database. Your local password
// is kept out of source control in appsettings.Local.json ("MySqlPassword") and is
// injected into the placeholder here. (A full DefaultConnection in Local.json still
// works and takes precedence, for backward compatibility.)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var localPassword = builder.Configuration["MySqlPassword"];
if (!string.IsNullOrWhiteSpace(localPassword) && connectionString != null && connectionString.Contains("YOUR_MYSQL_PASSWORD"))
    connectionString = connectionString.Replace("YOUR_MYSQL_PASSWORD", localPassword);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<SystemNotificationService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // re-check the account on every request so a deactivated user
        // (or a stale role claim) doesn't keep working on an old cookie
        options.Events.OnValidatePrincipal = async context =>
        {
            var idStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int userId))
            {
                context.RejectPrincipal();
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var account = await db.tblUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId);

            if (account == null || !account.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            // rebuild the cookie if the username or role changed since login
            if (context.Principal?.FindFirstValue(ClaimTypes.Role) != account.Role ||
                context.Principal?.FindFirstValue(ClaimTypes.Name) != account.Username)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, account.UserID.ToString()),
                    new(ClaimTypes.Name, account.Username),
                    new(ClaimTypes.Role, account.Role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                context.ReplacePrincipal(new ClaimsPrincipal(identity));
                context.ShouldRenew = true;
            }
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // The database is no longer conjured from the models at startup. It is a real
    // MySQL database, built once by running Database/ynclino_schema.sql, and the
    // program now only connects to what is already there.
    //
    // EnsureCreated() used to sit here. It was removed on purpose: it silently
    // created whatever the models happened to say, so the running schema and the
    // script could drift apart without anyone noticing, and it does nothing at all
    // once the database exists. If the database is missing, we want to say so
    // plainly rather than invent one.
    if (!db.Database.CanConnect())
    {
        Console.WriteLine("[schema] Cannot reach the database named in the connection string.");
        Console.WriteLine("[schema] Create it first:  mysql -u root -p < Database/ynclino_schema.sql");
    }
    // A database built by Database/ynclino_schema.sql already has every table and
    // column the application expects, so there is nothing to patch at startup.
    // What used to live here — sixteen AddColumnIfMissing calls, a CREATE TABLE
    // for tblPayments, and a run of one-time backfills and label migrations
    // (Low/Medium/High to Minor/Moderate/Major, "Late" to "Overdue", "Vacant" to
    // "Available") — existed only to repair databases that EnsureCreated() had
    // conjured from whatever the models said at the time. Those databases are no
    // longer how this system is set up, and on a script-built one every one of
    // those statements matched nothing. They are gone.
    //
    // All that is left is the check below: read one row from each table, so a
    // database built from an out-of-date script is reported at startup rather
    // than failing later on some page nobody has opened yet.
    try
    {
        // AsNoTracking: probing must not leave stale entities in the change tracker.
        db.tblTenants.AsNoTracking().FirstOrDefault();
        db.tblUnits.AsNoTracking().FirstOrDefault();
        db.tblUsers.AsNoTracking().FirstOrDefault();
        db.tblBillings.AsNoTracking().FirstOrDefault();
        db.tblPayments.AsNoTracking().FirstOrDefault();
        db.tblMaintenanceRequests.AsNoTracking().FirstOrDefault();
        db.tblLostFoundItems.AsNoTracking().FirstOrDefault();
        db.tblClaimRequests.AsNoTracking().FirstOrDefault();
        db.tblUnitTransferRequests.AsNoTracking().FirstOrDefault();
    }
    catch (Exception ex)
    {
        db.ChangeTracker.Clear();

        Console.WriteLine("==================================================================");
        Console.WriteLine(" SCHEMA MISMATCH - the database does not match the current models.");
        Console.WriteLine(" " + ex.Message);
        Console.WriteLine(" NOTHING WAS DELETED. Rebuild it from the script:");
        Console.WriteLine("   mysql -u root -p < Database/ynclino_schema.sql");
        Console.WriteLine("==================================================================");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
