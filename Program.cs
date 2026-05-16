using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Use an absolute SQLite path inside the app's content root so it works
// the same locally and in containers (Railway, Docker, etc.)
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "YnclinoAMS.db");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// Auto-create the database on first run (no migrations needed)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// Show detailed errors everywhere for now so deployment issues are visible.
// Replace with the friendly Error page once the app is stable.
app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
