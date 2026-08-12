using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private async Task<tblTenant?> GetCurrentTenantAsync()
        {
            var uid = CurrentUserID();
            if (uid == null) return null;
            return await _context.tblTenants.FirstOrDefaultAsync(t => t.UserID == uid && t.Status == "Active");
        }

        // the billing status is derived from how much has been paid and the due date:
        //   Paid    – paid in full
        //   Partial – part paid, balance remains, not yet past due
        //   Unpaid  – nothing paid, not yet past due
        //   Overdue    – past the due date and not paid in full
        public static string DeriveStatus(decimal amountDue, decimal? amountPaid, DateTime dueDate)
        {
            decimal paid = amountPaid ?? 0m;
            if (paid >= amountDue) return "Paid";
            if (dueDate.Date < DateTime.Today) return "Overdue";
            if (paid > 0m) return "Partial";
            return "Unpaid";
        }

        // recompute the status of every not-fully-paid bill so "Overdue" stays current
        private async Task RefreshStatusesAsync(int? tenantId = null)
        {
            var open = await _context.tblBillings
                .Where(b => (b.AmountPaid == null || b.AmountPaid < b.AmountDue)
                            && (tenantId == null || b.TenantID == tenantId))
                .ToListAsync();

            bool changed = false;
            foreach (var bill in open)
            {
                var status = DeriveStatus(bill.AmountDue, bill.AmountPaid, bill.DueDate);
                if (bill.Status != status) { bill.Status = status; changed = true; }
            }
            if (changed) await _context.SaveChangesAsync();
        }

        // GET: Billing
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            await RefreshStatusesAsync();

            IQueryable<tblBilling> query = _context.tblBillings
                .Include(b => b.Tenant).ThenInclude(t => t!.Unit);

            // tenants only see their own bills
            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return View(new List<tblBilling>());
                query = query.Where(b => b.TenantID == tenant.TenantID);
            }

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(b => b.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(b =>
                    b.Tenant!.FirstName.Contains(searchTerm) ||
                    b.Tenant!.LastName.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;
            var uid = CurrentUserID();
            ViewBag.UnreadIds = uid == null ? new HashSet<int>()
                : await NotificationHelper.UnreadTargetIdsAsync(_context, uid.Value, "Billing");
            ViewBag.ReadIds = uid == null ? new HashSet<int>()
                : await NotificationHelper.ReadTargetIdsAsync(_context, uid.Value, "Billing");

            return View(await query.OrderByDescending(b => b.BillingPeriod).ToListAsync());
        }

        // GET: Billing/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();

            // tenant can only see their own
            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null || billing.TenantID != tenant.TenantID) return Forbid();
            }

            var uid = CurrentUserID();
            if (uid != null) await NotificationHelper.MarkRecordReadAsync(_context, uid.Value, "Billing", billing.BillingID);

            return View(billing);
        }

        // GET: Billing/Create
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
                DueDate = vm.DueDate,
                Status = DeriveStatus(vm.AmountDue, null, vm.DueDate),
                Notes = vm.Notes,
                DateIssued = DateTime.Now
            };

            _context.tblBillings.Add(billing);
            await _context.SaveChangesAsync();

            // let the tenant know a new bill was issued
            var billedTenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == vm.TenantID);
            if (billedTenant?.UserID != null)
                await NotificationHelper.CreateAsync(_context, billedTenant.UserID.Value, "Billing",
                    $"A bill of ₱{billing.AmountDue:N2} for {period:MMMM yyyy} was issued (due {billing.DueDate:MMM dd}).",
                    $"/Billing/Details/{billing.BillingID}", billing.BillingID);

            TempData["Success"] = "Billing record created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/Edit/5
        [Authorize(Roles = "Admin")]
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
                DueDate = billing.DueDate,
                AmountPaid = billing.AmountPaid,
                DatePaid = billing.DatePaid,
                Status = billing.Status,
                Notes = billing.Notes,
                AvailableTenants = await GetTenantListForBillAsync(billing.TenantID)
            };
            return View(vm);
        }

        // POST: Billing/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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
            billing.DueDate = vm.DueDate;
            billing.AmountPaid = vm.AmountPaid;
            billing.Status = DeriveStatus(vm.AmountDue, vm.AmountPaid, vm.DueDate);
            billing.Notes = vm.Notes;

            // record a payment date when something has been paid, clear it otherwise
            billing.DatePaid = (vm.AmountPaid.HasValue && vm.AmountPaid.Value > 0)
                ? (vm.DatePaid ?? DateTime.Today)
                : null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Billing record updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var billing = await _context.tblBillings
                .Include(b => b.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(b => b.BillingID == id);

            if (billing == null) return NotFound();
            return View(billing);
        }

        // POST: Billing/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            _context.tblBillings.Remove(billing);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Billing record deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ajax helper - suggests the unit's monthly rent and reports any arrears
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSuggestedAmount(int tenantId)
        {
            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenantID == tenantId);
            if (tenant == null || tenant.Unit == null)
                return Json(new { suggestedAmount = 0m, unpaidMonths = 0 });

            var unpaidCount = await _context.tblBillings
                .CountAsync(b => b.TenantID == tenantId && (b.AmountPaid == null || b.AmountPaid < b.AmountDue));

            // each month is its own bill, so suggest one month's rent
            return Json(new {
                suggestedAmount = tenant.Unit.RentPrice,
                unpaidMonths = unpaidCount
            });
        }

        private async Task<IEnumerable<SelectListItem>> GetActiveTenantListAsync()
        {
            return await _context.tblTenants
                .Include(t => t.Unit)
                .Where(t => t.Status == "Active" && t.UnitID != null)
                .OrderBy(t => t.LastName)
                .Select(t => new SelectListItem
                {
                    Value = t.TenantID.ToString(),
                    Text = $"{t.LastName}, {t.FirstName} — Unit {t.Unit!.UnitNumber}"
                })
                .ToListAsync();
        }

        // like the active list, but always includes the bill's own tenant even if they moved out
        private async Task<IEnumerable<SelectListItem>> GetTenantListForBillAsync(int currentTenantId)
        {
            return await _context.tblTenants
                .Include(t => t.Unit)
                .Where(t => (t.Status == "Active" && t.UnitID != null) || t.TenantID == currentTenantId)
                .OrderBy(t => t.LastName)
                .Select(t => new SelectListItem
                {
                    Value = t.TenantID.ToString(),
                    Text = $"{t.LastName}, {t.FirstName} — Unit {t.Unit!.UnitNumber}"
                })
                .ToListAsync();
        }
    }
}
