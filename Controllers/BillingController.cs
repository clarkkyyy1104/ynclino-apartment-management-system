using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
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

        // GET: Billing
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
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
            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            var billing = new tblBilling
            {
                TenantID = vm.TenantID,
                BillingPeriod = new DateTime(vm.BillingPeriod.Year, vm.BillingPeriod.Month, 1),
                AmountDue = vm.AmountDue,
                DueDate = vm.DueDate,
                Status = "Unpaid",
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
                DueDate = billing.DueDate,
                AmountPaid = billing.AmountPaid,
                DatePaid = billing.DatePaid,
                Status = billing.Status,
                Notes = billing.Notes,
                AvailableTenants = await GetActiveTenantListAsync()
            };
            return View(vm);
        }

        // POST: Billing/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BillingViewModel vm)
        {
            if (id != vm.BillingID) return NotFound();
            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            billing.TenantID = vm.TenantID;
            billing.BillingPeriod = new DateTime(vm.BillingPeriod.Year, vm.BillingPeriod.Month, 1);
            billing.AmountDue = vm.AmountDue;
            billing.DueDate = vm.DueDate;
            billing.AmountPaid = vm.AmountPaid;
            billing.DatePaid = vm.DatePaid;
            billing.Status = vm.Status;
            billing.Notes = vm.Notes;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Billing record updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/Delete/5
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            _context.tblBillings.Remove(billing);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Billing record deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ajax helper - suggested amount = unpaid months times unit price
        [HttpGet]
        public async Task<IActionResult> GetSuggestedAmount(int tenantId)
        {
            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenantID == tenantId);
            if (tenant == null || tenant.Unit == null)
                return Json(new { unitPrice = 0m, suggestedAmount = 0m, unpaidMonths = 0 });

            var unpaidCount = await _context.tblBillings
                .CountAsync(b => b.TenantID == tenantId && (b.Status == "Unpaid" || b.Status == "Overdue"));

            // current month plus whatever's outstanding
            var months = unpaidCount + 1;
            return Json(new {
                unitPrice = tenant.Unit.RentPrice,
                unpaidMonths = unpaidCount,
                suggestedAmount = tenant.Unit.RentPrice * months
            });
        }

        private async Task<IEnumerable<SelectListItem>> GetActiveTenantListAsync()
        {
            return await _context.tblTenants
                .Include(t => t.Unit)
                .Where(t => t.Status == "Active")
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
