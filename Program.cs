using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Filters;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;

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

    // Create the database and schema if it doesn't exist yet. On a brand-new
    // MySQL server this builds every table from the current models.
    db.Database.EnsureCreated();

    // Guard against a leftover database whose schema predates the current
    // models: probe every table, and if the shape no longer matches, rebuild
    // it from scratch. A fresh, matching database never triggers this.
    // AsNoTracking: probing must not leave stale entities in the change tracker.
    // If a probe fails partway and we rebuild below, any rows already read would
    // otherwise collide (same key) with the freshly-seeded rows on SaveChanges.
    void ProbeSchema()
    {
        db.tblTenants.AsNoTracking().FirstOrDefault();
        db.tblUnits.AsNoTracking().FirstOrDefault();
        db.tblUsers.AsNoTracking().FirstOrDefault();
        db.tblBillings.AsNoTracking().FirstOrDefault();
        db.tblMaintenanceRequests.AsNoTracking().FirstOrDefault();
        db.tblLostFoundItems.AsNoTracking().FirstOrDefault();
        db.tblClaimRequests.AsNoTracking().FirstOrDefault();
        db.tblUnitTransferRequests.AsNoTracking().FirstOrDefault();
        db.tblNotifications.AsNoTracking().FirstOrDefault();
    }

    try
    {
        ProbeSchema();
    }
    catch (Exception)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        // drop anything the failed probe may have tracked before it threw
        db.ChangeTracker.Clear();
    }

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

    // migrate any maintenance rows still using the old priority labels to the
    // current vocabulary (Low->Minor, Medium->Moderate, High->Major; Urgent kept)
    if (db.tblMaintenanceRequests.Any(m => m.Priority == "Low" || m.Priority == "Medium" || m.Priority == "High"))
    {
        db.tblMaintenanceRequests.Where(m => m.Priority == "Low").ExecuteUpdate(s => s.SetProperty(m => m.Priority, "Minor"));
        db.tblMaintenanceRequests.Where(m => m.Priority == "Medium").ExecuteUpdate(s => s.SetProperty(m => m.Priority, "Moderate"));
        db.tblMaintenanceRequests.Where(m => m.Priority == "High").ExecuteUpdate(s => s.SetProperty(m => m.Priority, "Major"));
    }

    // billing status label follows the manuscript: the past-due state is "Overdue"
    // (older databases stored it as "Late") — migrate any leftover rows
    if (db.tblBillings.Any(b => b.Status == "Late"))
        db.tblBillings.Where(b => b.Status == "Late").ExecuteUpdate(s => s.SetProperty(b => b.Status, "Overdue"));

    // unit occupancy label follows the manuscript: an empty unit is "Available"
    // (older databases stored it as "Vacant") — migrate any leftover rows
    if (db.tblUnits.Any(u => u.Status == "Vacant"))
        db.tblUnits.Where(u => u.Status == "Vacant").ExecuteUpdate(s => s.SetProperty(u => u.Status, "Available"));

    // the forced password change is for tenants only — clear the flag on any admin
    // account that may have picked it up before this rule was enforced
    if (db.tblUsers.Any(u => u.Role == "Admin" && u.MustChangePassword))
        db.tblUsers.Where(u => u.Role == "Admin" && u.MustChangePassword)
                   .ExecuteUpdate(s => s.SetProperty(u => u.MustChangePassword, false));

    // a tenant may now exist without a unit (they apply for one), so make these
    // columns nullable on databases created before the change. No-op when already null.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE tblTenants MODIFY UnitID INT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE tblUnitTransferRequests MODIFY CurrentUnitID INT NULL"); } catch { }
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
