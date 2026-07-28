using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    db.Database.EnsureCreated();

    // If an older database is missing columns that the models now expect,
    // patch it in place so existing data survives; only rebuild from scratch
    // when patching isn't enough. Normal runs are untouched.
    void ProbeSchema()
    {
        db.tblTenants.FirstOrDefault();
        db.tblUnits.FirstOrDefault();
        db.tblUsers.FirstOrDefault();
        db.tblBillings.FirstOrDefault();
        db.tblMaintenanceRequests.FirstOrDefault();
        db.tblLostFoundItems.FirstOrDefault();
        db.tblClaimRequests.FirstOrDefault();
    }

    try
    {
        ProbeSchema();
    }
    catch (Microsoft.Data.Sqlite.SqliteException)
    {
        var patches = new[]
        {
            "ALTER TABLE tblTenants ADD COLUMN EmergencyContactName TEXT",
            "ALTER TABLE tblTenants ADD COLUMN EmergencyContactRelationship TEXT",
            "ALTER TABLE tblTenants ADD COLUMN EmergencyContactNumber TEXT",
            "ALTER TABLE tblUnits ADD COLUMN Deposit TEXT NOT NULL DEFAULT '0.0'",
            // the primary-admin flag was renamed IsSuperAdmin -> IsMainAdmin;
            // rename in place to keep the existing flag, falling back to adding
            // the column if an older database never had it
            "ALTER TABLE tblUsers RENAME COLUMN IsSuperAdmin TO IsMainAdmin",
            "ALTER TABLE tblUsers ADD COLUMN IsMainAdmin INTEGER NOT NULL DEFAULT 0"
        };
        foreach (var sql in patches)
        {
            try { db.Database.ExecuteSqlRaw(sql); }
            catch (Microsoft.Data.Sqlite.SqliteException) { /* column already exists */ }
        }

        try
        {
            ProbeSchema();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }
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
