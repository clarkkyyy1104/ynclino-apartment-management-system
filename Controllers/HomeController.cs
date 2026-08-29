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

            // ── Payments ─────────────────────────────────────────────
            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var payments = await _context.tblBillings
                .Select(b => new { b.AmountPaid, b.BillingPeriod })
                .ToListAsync();

            ViewBag.PaymentsCount = payments.Count;
            ViewBag.CollectedTotal = payments.Sum(p => p.AmountPaid);
            ViewBag.CollectedThisMonth = payments
                .Where(p => p.BillingPeriod.Year == thisMonth.Year && p.BillingPeriod.Month == thisMonth.Month)
                .Sum(p => p.AmountPaid);

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
