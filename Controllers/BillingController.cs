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

        // GET: Billing/Edit/5  — top up a bill that was only partially paid
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();

            decimal rent = billing.Tenant?.MonthlyRent ?? 0m;
            decimal remaining = rent - billing.AmountPaid;

            // only a partial bill (something paid, but still short of the rent) can be topped up
            if (remaining <= 0m)
            {
                TempData["Error"] = "This bill is already fully paid — there is nothing left to update.";
                return RedirectToAction(nameof(Details), new { id = billing.BillingID });
            }

            var vm = new BillingViewModel
            {
                BillingID = billing.BillingID,
                TenantID = billing.TenantID,
                TenantName = billing.Tenant?.FullName,
                BillingPeriod = billing.BillingPeriod,
                PaymentMethod = billing.PaymentMethod,
                TenantRent = rent,
                AlreadyPaid = billing.AmountPaid,
                RemainingBalance = remaining,
                AmountPaid = 0m   // the ADDITIONAL amount being recorded now
            };
            return View(vm);
        }

        // POST: Billing/Edit/5  — apply the extra payment, and roll any excess into a new bill
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BillingViewModel vm)
        {
            if (id != vm.BillingID) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillingID == id);
            if (billing == null) return NotFound();

            decimal rent = billing.Tenant?.MonthlyRent ?? 0m;
            decimal remaining = rent - billing.AmountPaid;

            if (remaining <= 0m)
            {
                TempData["Error"] = "This bill is already fully paid — there is nothing left to update.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                vm.TenantName = billing.Tenant?.FullName;
                vm.BillingPeriod = billing.BillingPeriod;
                vm.TenantRent = rent;
                vm.AlreadyPaid = billing.AmountPaid;
                vm.RemainingBalance = remaining;
                return View(vm);
            }

            decimal additional = vm.AmountPaid;

            // 1) settle this bill's remaining balance first
            decimal appliedHere = Math.Min(additional, remaining);
            billing.AmountPaid += appliedHere;
            if (!string.IsNullOrEmpty(vm.PaymentMethod))
                billing.PaymentMethod = vm.PaymentMethod;

            // 2) anything paid beyond this bill's balance rolls into new bill(s) for the
            //    following month(s), so no single bill's Amount Paid ever exceeds the rent
            decimal excess = additional - appliedHere;
            int newBills = 0;
            var period = billing.BillingPeriod;
            while (excess > 0m && rent > 0m)
            {
                period = period.AddMonths(1);
                decimal take = Math.Min(excess, rent);
                _context.tblBillings.Add(new tblBilling
                {
                    TenantID = billing.TenantID,
                    BillingPeriod = period,
                    AmountPaid = take,
                    PaymentMethod = billing.PaymentMethod,
                    Notes = $"Carried over from {billing.BillingPeriod:MMMM yyyy} overpayment.",
                    DateIssued = DateTime.Now
                });
                excess -= take;
                newBills++;
            }

            await _context.SaveChangesAsync();

            if (newBills > 0)
                TempData["Success"] = $"Payment recorded — this bill is now fully paid, and ₱{(additional - appliedHere):N0} rolled over into {newBills} new bill(s) for the following month(s).";
            else if (billing.AmountPaid >= rent)
                TempData["Success"] = "Payment recorded — this bill is now fully paid.";
            else
                TempData["Success"] = $"Payment recorded. Remaining balance: ₱{(rent - billing.AmountPaid):N0}.";

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
