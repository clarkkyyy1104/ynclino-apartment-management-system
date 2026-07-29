using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// Per-developer overrides (e.g. your local MySQL password) live in appsettings.Local.json,
// which is git-ignored so secrets never get committed. It overrides appsettings.json when present.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
    catch (Exception)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
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
