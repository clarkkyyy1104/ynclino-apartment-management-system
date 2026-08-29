using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // the billing status is derived from how much has been paid:
        //   Paid    – paid in full
        //   Partial – part paid, balance remains
        //   Unpaid  – nothing paid
        public static string DeriveStatus(decimal amountDue, decimal? amountPaid)
        {
            decimal paid = amountPaid ?? 0m;
            if (paid >= amountDue) return "Paid";
            if (paid > 0m) return "Partial";
            return "Unpaid";
        }

        // GET: Billing
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            IQueryable<tblBilling> query = _context.tblBillings.Include(b => b.Tenant);

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(b => b.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(b => b.Tenant!.Name.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

            return View(await query.OrderByDescending(b => b.BillingPeriod).ToListAsync());
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

        // POST: Billing/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BillingViewModel vm)
        {
            var period = new DateTime(vm.BillingPeriod.Year, vm.BillingPeriod.Month, 1);

            // one bill per tenant per month
            bool alreadyBilled = await _context.tblBillings
                .AnyAsync(b => b.TenantID == vm.TenantID && b.BillingPeriod == period);
            if (alreadyBilled)
                ModelState.AddModelError(string.Empty, "This tenant already has a bill for the selected month.");

            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            var billing = new tblBilling
            {
                TenantID = vm.TenantID,
                BillingPeriod = period,
                AmountDue = vm.AmountDue,
                Status = DeriveStatus(vm.AmountDue, null),
                Notes = vm.Notes,
                DateIssued = DateTime.Now
            };

            _context.tblBillings.Add(billing);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Billing record created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();

            var vm = new BillingViewModel
            {
                BillingID = billing.BillingID,
                TenantID = billing.TenantID,
                TenantName = billing.Tenant?.FullName,
                BillingPeriod = billing.BillingPeriod,
                AmountDue = billing.AmountDue,
                AmountPaid = billing.AmountPaid,
                DatePaid = billing.DatePaid,
                PaymentMethod = billing.PaymentMethod,
                Status = billing.Status,
                Notes = billing.Notes,
                AvailableTenants = await GetTenantListForBillAsync(billing.TenantID)
            };
            return View(vm);
        }

        // POST: Billing/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BillingViewModel vm)
        {
            if (id != vm.BillingID) return NotFound();

            if (vm.AmountPaid != null && vm.AmountPaid < 0)
                ModelState.AddModelError("AmountPaid", "Amount paid cannot be negative.");

            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetTenantListForBillAsync(vm.TenantID);
                return View(vm);
            }

            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            billing.TenantID = vm.TenantID;
            billing.BillingPeriod = new DateTime(vm.BillingPeriod.Year, vm.BillingPeriod.Month, 1);
            billing.AmountDue = vm.AmountDue;
            billing.AmountPaid = vm.AmountPaid;
            billing.Status = DeriveStatus(vm.AmountDue, vm.AmountPaid);
            billing.Notes = vm.Notes;

            // record a payment date and method when something has been paid, clear them otherwise
            bool hasPayment = vm.AmountPaid.HasValue && vm.AmountPaid.Value > 0;
            billing.DatePaid = hasPayment ? (vm.DatePaid ?? DateTime.Today) : null;
            billing.PaymentMethod = hasPayment ? vm.PaymentMethod : null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Billing record updated.";
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
            TempData["Success"] = "Billing record deleted.";
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

        // like the active list, but always includes the bill's own tenant even if they moved out
        private async Task<IEnumerable<SelectListItem>> GetTenantListForBillAsync(int currentTenantId)
        {
            return await _context.tblTenants
                .Where(t => t.Status == "Active" || t.TenantID == currentTenantId)
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
