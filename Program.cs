using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;

var builder = WebApplication.CreateBuilder(args);

// Only override the URL on Railway (PORT is set by Railway, not locally)
var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (railwayPort != null)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

builder.Services.AddControllersWithViews();

// Windows (local dev): DB sits in the project root beside the .csproj
// Linux (Railway):      DB sits in /tmp (writable on any container)
var dbDir = OperatingSystem.IsWindows()
    ? Directory.GetCurrentDirectory()
    : "/tmp";
var dbPath = Path.Combine(dbDir, "YnclinoAMS.db");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// Auto-create tables on first run — no migrations needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
