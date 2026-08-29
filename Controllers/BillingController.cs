using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    // Billing is a simple payment log: the admin records what a tenant paid for a month.
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Billing
        public async Task<IActionResult> Index(string? searchTerm)
        {
            IQueryable<tblBilling> query = _context.tblBillings.Include(b => b.Tenant);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(b => b.Tenant!.Name.Contains(searchTerm));

            ViewBag.SearchTerm = searchTerm;

            return View(await query
                .OrderByDescending(b => b.BillingPeriod)
                .ThenByDescending(b => b.DateIssued)
                .ToListAsync());
        }

        // GET: Billing/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();
            return View(billing);
        }

        // GET: Billing/Create
        public async Task<IActionResult> Create()
        {
            var vm = new BillingViewModel
            {
                AvailableTenants = await GetActiveTenantListAsync()
            };
            return View(vm);
        }

        // POST: Billing/Create  — records a tenant's payment for a month
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BillingViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            var billing = new tblBilling
            {
                TenantID = vm.TenantID,
                BillingPeriod = new DateTime(vm.BillingPeriod.Year, vm.BillingPeriod.Month, 1),
                AmountPaid = vm.AmountPaid,
                PaymentMethod = vm.PaymentMethod,
                Notes = vm.Notes,
                DateIssued = DateTime.Now
            };

            _context.tblBillings.Add(billing);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment recorded.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();
            return View(billing);
        }

        // POST: Billing/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            _context.tblBillings.Remove(billing);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Payment record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetActiveTenantListAsync()
        {
            return await _context.tblTenants
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.TenantID.ToString(),
                    Text = t.MonthlyRent > 0 ? $"{t.Name} — ₱{t.MonthlyRent:N0}/mo" : t.Name
                })
                .ToListAsync();
        }
    }
}
