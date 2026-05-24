using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Models;

namespace YnclinoAMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SemiAdmin"))
            {
                ViewBag.TotalUnits        = await _context.tblUnits.CountAsync();
                ViewBag.VacantUnits       = await _context.tblUnits.CountAsync(u => u.Status == "Vacant");
                ViewBag.OccupiedUnits     = await _context.tblUnits.CountAsync(u => u.Status == "Occupied");
                ViewBag.MaintenanceUnits  = await _context.tblUnits.CountAsync(u => u.Status == "Under Maintenance");
                ViewBag.ActiveTenants     = await _context.tblTenants.CountAsync(t => t.Status == "Active");
                ViewBag.InactiveTenants   = await _context.tblTenants.CountAsync(t => t.Status == "Inactive");
                ViewBag.TotalUsers        = await _context.tblUsers.CountAsync(u => u.IsActive);
                return View("AdminDashboard");
            }
            else
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                tblTenant? tenant = null;
                if (int.TryParse(userIdStr, out int userId))
                {
                    tenant = await _context.tblTenants
                        .Include(t => t.Unit)
                        .FirstOrDefaultAsync(t => t.UserID == userId && t.Status == "Active");
                }
                return View("TenantDashboard", tenant);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
