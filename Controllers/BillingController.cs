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
                ViewBag.AdvanceCredit = tenant.AdvanceCredit;
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

        //GET: Billing/History - read-only log of payments that have been recorded.
        //Admins see every payment; a tenant sees only their own.
        public async Task<IActionResult> History(string? searchTerm)
        {
            // A tenant only ever has their own payments, so send them straight to the detail page
            if (User.IsInRole("Tenant"))
            {
                var me = await GetCurrentTenantAsync();
                if (me == null) return View(new List<TenantPaymentSummary>());
                return RedirectToAction(nameof(TenantHistory), new { id = me.TenantID });
            }

            // Admin sees ONE ROW PER TENANT — cleaner than a long list of every payment.
            IQueryable<tblTenant> tenants = _context.tblTenants.Include(t => t.Unit);
            if (!string.IsNullOrWhiteSpace(searchTerm))
                tenants = tenants.Where(t => t.FirstName.Contains(searchTerm) || t.LastName.Contains(searchTerm));

            var list = await tenants.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToListAsync();

            var summaries = new List<TenantPaymentSummary>();
            foreach (var t in list)
            {
                var pays = await _context.tblPayments
                    .Where(p => p.Billing!.TenantID == t.TenantID)
                    .ToListAsync();

                summaries.Add(new TenantPaymentSummary
                {
                    TenantID = t.TenantID,
                    TenantName = t.FullName,
                    UnitNumber = t.Unit?.UnitNumber,
                    PaymentCount = pays.Count,
                    TotalPaid = pays.Sum(p => p.Amount),
                    LastPaymentDate = pays.Count > 0 ? pays.Max(p => p.DatePaid) : (DateTime?)null
                });
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.TotalCollected = summaries.Sum(x => x.TotalPaid);
            return View(summaries);
        }

        // GET: Billing/TenantHistory/5 — every payment made by one tenant
        public async Task<IActionResult> TenantHistory(int id)
        {
            // a tenant may only open their own history
            if (User.IsInRole("Tenant"))
            {
                var me = await GetCurrentTenantAsync();
                if (me == null || me.TenantID != id) return Forbid();
            }

            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenantID == id);
            if (tenant == null) return NotFound();

            var payments = await _context.tblPayments
                .Include(p => p.Billing).ThenInclude(b => b!.Tenant).ThenInclude(t => t!.Unit)
                .Where(p => p.Billing!.TenantID == id)
                .OrderByDescending(p => p.DatePaid).ThenByDescending(p => p.PaymentID)
                .ToListAsync();

            // running balance: how much of that bill was still owed AFTER each payment
            var balanceAfter = new Dictionary<int, decimal>();
            foreach (var billGroup in payments.GroupBy(p => p.BillingID))
            {
                decimal due = billGroup.First().Billing?.AmountDue ?? 0m;
                decimal running = 0m;
                foreach (var p in billGroup.OrderBy(p => p.DatePaid).ThenBy(p => p.PaymentID))
                {
                    running += p.Amount;
                    balanceAfter[p.PaymentID] = due - running;
                }
            }

            ViewBag.Tenant = tenant;
            ViewBag.TotalCollected = payments.Sum(p => p.Amount);
            ViewBag.BalanceAfter = balanceAfter;
            return View(payments);
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

            // if the tenant is holding advance payment, use it on this new bill
            decimal usedAdvance = await UseAdvanceCreditAsync(billing);

            // let the tenant know a new bill was issued
            var billedTenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == vm.TenantID);
            if (billedTenant?.UserID != null)
                await NotificationHelper.CreateAsync(_context, billedTenant.UserID.Value, "Billing",
                    $"A bill of ₱{billing.AmountDue:N0} for {period:MMMM yyyy} was issued (due {billing.DueDate:MMM dd}).",
                    $"/Billing/Details/{billing.BillingID}", billing.BillingID);

            TempData["Success"] = usedAdvance > 0
                ? $"Billing record created. ₱{usedAdvance:N0} of advance payment was applied automatically."
                : "Billing record created.";
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

            // a fully settled bill is closed history — view only.
            // use whichever total is higher so legacy bills (paid before the payment
            // history existed) are also recognised as settled
            decimal settled = Math.Max(billing.AmountPaid ?? 0m, await TotalPaidAsync(billing.BillingID));
            if (billing.AmountDue - settled <= 0)
            {
                TempData["Error"] = "This bill is fully paid and can no longer be updated.";
                return RedirectToAction(nameof(Details), new { id = billing.BillingID });
            }

            var vm = new BillingViewModel
            {
                BillingID = billing.BillingID,
                TenantID = billing.TenantID,
                TenantName = billing.Tenant?.FullName,
                Deposit = billing.Deposit,
                Advance = billing.Advance,
                BillingPeriod = billing.BillingPeriod,
                AmountDue = billing.AmountDue,
                DueDate = billing.DueDate,
                AmountPaid = billing.AmountPaid,
                DatePaid = billing.DatePaid,
                Status = billing.Status,
                Notes = billing.Notes,
                AdvanceCredit = billing.Tenant?.AdvanceCredit ?? 0m,
                TotalPaid = await TotalPaidAsync(billing.BillingID),
                Payments = await _context.tblPayments
                    .Where(p => p.BillingID == billing.BillingID)
                    .OrderByDescending(p => p.DatePaid).ThenByDescending(p => p.PaymentID)
                    .ToListAsync(),
                AvailableTenants = await GetTenantListForBillAsync(billing.TenantID)
            };
            return View(vm);
        }

        // the running total actually received for a bill = the sum of its payment rows
        private async Task<decimal> TotalPaidAsync(int billingId) =>
            await _context.tblPayments
                .Where(p => p.BillingID == billingId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // re-derive the bill's cached totals from its payment rows, so AmountPaid,
        // DatePaid and Status always agree with the payment history
        private async Task RefreshBillTotalsAsync(tblBilling bill)
        {
            var payments = await _context.tblPayments
                .Where(p => p.BillingID == bill.BillingID)
                .ToListAsync();

            decimal total = payments.Sum(p => p.Amount);
            bill.AmountPaid = total > 0 ? total : (decimal?)null;
            bill.DatePaid = payments.Count > 0 ? payments.Max(p => p.DatePaid) : (DateTime?)null;
            bill.Status = DeriveStatus(bill.AmountDue, bill.AmountPaid, bill.DueDate);
        }

        // Applies a payment: it settles THIS bill up to what it owes, and anything paid
        // beyond that is kept on the tenant as advance payment, which is deducted
        // automatically from their next bill.
        private async Task<(decimal here, decimal advance)> ApplyPaymentAsync(
            tblBilling bill, decimal amount, string? remarks)
        {
            decimal left = amount;

            // 1) this bill, up to what it still owes
            decimal balanceHere = bill.AmountDue - await TotalPaidAsync(bill.BillingID);
            decimal here = Math.Min(left, Math.Max(balanceHere, 0m));
            if (here > 0)
            {
                _context.tblPayments.Add(new tblPayment
                {
                    BillingID = bill.BillingID,
                    Amount = here,
                    Method = "Cash",
                    Remarks = remarks,
                    DatePaid = DateTime.Today,
                    RecordedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                await RefreshBillTotalsAsync(bill);
                await _context.SaveChangesAsync();
                left -= here;
            }

            // 2) anything paid beyond this month's bill becomes advance payment held for
            //    the tenant — it is deducted automatically from their next bill
            if (left > 0)
            {
                var tenant = await _context.tblTenants.FindAsync(bill.TenantID);
                if (tenant != null)
                {
                    tenant.AdvanceCredit += left;
                    await _context.SaveChangesAsync();
                }
            }

            return (here, left);
        }

        // Uses any advance payment the tenant is holding to settle a newly issued bill.
        private async Task<decimal> UseAdvanceCreditAsync(tblBilling bill)
        {
            var tenant = await _context.tblTenants.FindAsync(bill.TenantID);
            if (tenant == null || tenant.AdvanceCredit <= 0) return 0m;

            decimal balance = bill.AmountDue - await TotalPaidAsync(bill.BillingID);
            if (balance <= 0) return 0m;

            decimal use = Math.Min(tenant.AdvanceCredit, balance);
            _context.tblPayments.Add(new tblPayment
            {
                BillingID = bill.BillingID,
                Amount = use,
                Method = "Advance",
                Remarks = "Settled from advance payment",
                DatePaid = DateTime.Today,
                RecordedAt = DateTime.Now
            });
            tenant.AdvanceCredit -= use;
            await _context.SaveChangesAsync();
            await RefreshBillTotalsAsync(bill);
            await _context.SaveChangesAsync();
            return use;
        }

        // POST: Billing/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, BillingViewModel vm)
        {
            if (id != vm.BillingID) return NotFound();

            var billing = await _context.tblBillings.FindAsync(id);
            if (billing == null) return NotFound();

            decimal alreadyPaid = Math.Max(billing.AmountPaid ?? 0m, await TotalPaidAsync(id));
            decimal balanceBefore = billing.AmountDue - alreadyPaid;   // stored original, never the posted value

            // fully settled bills are view-only — reject the post outright
            if (balanceBefore <= 0)
            {
                TempData["Error"] = "This bill is fully paid and can no longer be updated.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Overpayment is allowed: anything beyond this bill spills onto the tenant's
            // other unpaid bills and then becomes advance payment. Only a negative
            // amount is rejected.
            if (vm.PaymentAmount.HasValue && vm.PaymentAmount.Value < 0)
                ModelState.AddModelError("PaymentAmount", "Payment cannot be negative.");

            if (!ModelState.IsValid)
            {
                vm.TotalPaid = alreadyPaid;
                vm.Payments = await _context.tblPayments
                    .Where(p => p.BillingID == id)
                    .OrderByDescending(p => p.DatePaid).ThenByDescending(p => p.PaymentID)
                    .ToListAsync();
                vm.AvailableTenants = await GetTenantListForBillAsync(vm.TenantID);
                return View(vm);
            }

            // 1) the bill's own details
            billing.TenantID = vm.TenantID;
            // BillingPeriod, AmountDue and DueDate are fixed at issue time - never overwritten here
            billing.Notes = vm.Notes;

            // 2) the payment is ADDED to the history, spilling onto older unpaid bills
            //    and finally into advance payment when it exceeds what is owed
            decimal paidNow = vm.PaymentAmount ?? 0m;
            bool paymentRecorded = paidNow > 0;
            decimal toAdvance = 0m;
            if (paymentRecorded)
                (_, toAdvance) = await ApplyPaymentAsync(billing, paidNow, vm.PaymentRemarks);

            // 3) recompute the running total, last payment date and status
            await RefreshBillTotalsAsync(billing);
            await _context.SaveChangesAsync();

            if (paymentRecorded)
            {
                decimal balanceAfter = billing.AmountDue - (billing.AmountPaid ?? 0m);

                string extra = toAdvance > 0
                    ? $" ₱{toAdvance:N0} became advance payment and will be deducted from the next bill."
                    : "";

                // tell the tenant their payment was posted
                var payer = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == billing.TenantID);
                if (payer?.UserID != null)
                    await NotificationHelper.CreateAsync(_context, payer.UserID.Value, "Billing",
                        $"Payment of ₱{paidNow:N0} received for {billing.BillingPeriod:MMMM yyyy}. Remaining balance: ₱{balanceAfter:N0}.{extra}",
                        $"/Billing/Details/{billing.BillingID}", billing.BillingID);

                TempData["Success"] = (balanceAfter <= 0
                    ? $"Payment of ₱{paidNow:N0} recorded. This bill is now fully paid."
                    : $"Payment of ₱{paidNow:N0} recorded. Remaining balance: ₱{balanceAfter:N0}.") + extra;
            }
            else
            {
                TempData["Success"] = "Billing record updated.";
            }

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

            // suggest the next month this tenant has not been billed for yet
            var latest = await _context.tblBillings
                .Where(b => b.TenantID == tenantId)
                .OrderByDescending(b => b.BillingPeriod)
                .Select(b => (DateTime?)b.BillingPeriod)
                .FirstOrDefaultAsync();

            var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var period = latest.HasValue ? latest.Value.AddMonths(1) : thisMonth;
            if (period < thisMonth) period = thisMonth;

            // each month is its own bill, so suggest one month's rent
            return Json(new {
                suggestedAmount = tenant.Unit.RentPrice,
                unpaidMonths = unpaidCount,
                suggestedPeriod = period.ToString("yyyy-MM"),
                suggestedDueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
                advanceCredit = tenant.AdvanceCredit
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
