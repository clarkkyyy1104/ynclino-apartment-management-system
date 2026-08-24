using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
            ViewBag.InactiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Inactive");

            // outstanding = bills that aren't paid in full
            var outstanding = await _context.tblBillings
                .Where(b => b.AmountPaid == null || b.AmountPaid < b.AmountDue)
                .Select(b => new { b.AmountDue, b.AmountPaid })
                .ToListAsync();

            ViewBag.OutstandingCount = outstanding.Count;
            ViewBag.OutstandingTotal = outstanding.Sum(b => b.AmountDue - (b.AmountPaid ?? 0m));

            return View("AdminDashboard");
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
