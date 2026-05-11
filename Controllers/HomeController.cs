using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Models;

namespace YnclinoAMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUnits = await _context.tblUnits.CountAsync();
            ViewBag.VacantUnits = await _context.tblUnits.CountAsync(u => u.Status == "Vacant");
            ViewBag.OccupiedUnits = await _context.tblUnits.CountAsync(u => u.Status == "Occupied");
            ViewBag.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
