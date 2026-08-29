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
            // ── Tenants ──────────────────────────────────────────────
            ViewBag.TotalTenants = await _context.tblTenants.CountAsync();
            ViewBag.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
            ViewBag.InactiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Inactive");

            // ── Billing & Payments ───────────────────────────────────
            var bills = await _context.tblBillings
                .Select(b => new { b.AmountDue, b.AmountPaid })
                .ToListAsync();

            var outstanding = bills.Where(b => (b.AmountPaid ?? 0m) < b.AmountDue).ToList();
            ViewBag.OutstandingCount = outstanding.Count;
            ViewBag.OutstandingTotal = outstanding.Sum(b => b.AmountDue - (b.AmountPaid ?? 0m));
            ViewBag.CollectedTotal = bills.Sum(b => b.AmountPaid ?? 0m);

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
