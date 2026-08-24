using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// Per-developer overrides (your local MySQL password) live in appsettings.Local.json,
// which is git-ignored so secrets never get committed.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllersWithViews();

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

    // Make sure the database and its tables exist, in a way that also works on
    // shared hosting where the database is pre-created (empty) and the DB user
    // cannot create or drop databases:
    //   • local dev — the database may not exist yet, so EnsureCreated builds it
    //     and every table from the current models.
    //   • shared hosting — the database already exists but is empty, so
    //     EnsureCreated does nothing; we then create just the tables.
    // Neither path drops data, so this is safe to run on every startup.
    var creator = db.GetService<IRelationalDatabaseCreator>();
    try { db.Database.EnsureCreated(); }
    catch { /* database already exists and we lack create-database rights — fine */ }
    try { if (!creator.HasTables()) creator.CreateTables(); }
    catch { /* the tables are already present */ }

    // create a default admin the first time the app runs
    if (!db.tblUsers.Any(u => u.Role == "Admin"))
    {
        db.tblUsers.Add(new tblUser
        {
            Username = "admin",
            Password = PasswordHelper.Hash("Admin@123"),
            Role = "Admin",
            IsActive = true,
            IsMainAdmin = true,
            DateCreated = DateTime.Now
        });
        db.SaveChanges();
    }

    // one-off label migration for databases created by older versions of the app
    // (a fresh database has nothing to migrate, so this simply no-ops)
    try
    {
        // billing status: the old "Overdue" is now "Late"
        if (db.tblBillings.Any(b => b.Status == "Overdue"))
            db.tblBillings.Where(b => b.Status == "Overdue").ExecuteUpdate(s => s.SetProperty(b => b.Status, "Late"));
    }
    catch { /* labels are already current — nothing to migrate */ }
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
