using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;

// When VS runs the app it sets CWD to bin\Debug\net8.0, not the project root.
// Walk up from the executable until we find the folder that contains wwwroot or a .csproj.
static string FindContentRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "wwwroot")) ||
            dir.GetFiles("*.csproj").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return AppContext.BaseDirectory;
}

var contentRoot = FindContentRoot();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args        = args,
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot")
});

// Only override the URL on Railway (PORT env var is set by Railway, not locally)
var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (railwayPort != null)
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");

builder.Services.AddControllersWithViews();

// Windows local dev  → DB in project root (next to .csproj)
// Linux / Railway    → DB in /tmp (always writable)
var dbDir  = OperatingSystem.IsWindows() ? contentRoot : "/tmp";
var dbPath = Path.Combine(dbDir, "YnclinoAMS.db");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// Auto-create all tables on first run — no migrations needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
