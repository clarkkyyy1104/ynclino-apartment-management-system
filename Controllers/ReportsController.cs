using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    // Reports & Records. Everything here is READ-ONLY — the system only displays
    // reports on screen; it never edits data and has no export or print feature.
    // maintenance staff only ever see their own assigned work — never the
    // apartment's reports. Locked at the controller so a typed URL fails too.
    [Authorize(Roles = "Admin,Tenant")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private async Task<tblTenant?> GetCurrentTenantAsync()
        {
            var uid = CurrentUserID();
            if (uid == null) return null;
            return await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.UserID == uid && t.Status == "Active");
        }

        // GET: Reports — the reports dashboard the manuscript asks for:
        // total units, occupancy status, monthly income, pending maintenance
        // requests, and pending payments / overdue tenants.
        public async Task<IActionResult> Index()
        {
            // a tenant only ever sees their own records
            if (User.IsInRole("Tenant"))
                return RedirectToAction(nameof(MyRecords));

            var vm = new ReportsViewModel();
            var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            // ── Units / occupancy ──
            var units = await _context.tblUnits.AsNoTracking().ToListAsync();
            vm.TotalUnits = units.Count;
            vm.OccupiedUnits = units.Count(u => u.Status == "Occupied");
            vm.AvailableUnits = units.Count(u => u.Status == "Available");
            vm.ReservedUnits = units.Count(u => u.Status == "Reserved");
            vm.MaintenanceUnits = units.Count(u => u.Status == "Under Maintenance");
            
            // The occupancy report used to be five counts and a percentage, which
            // says nothing a person can act on: knowing five units are empty is
            // no use without knowing WHICH five, or what leaving them empty costs.
            // Each state is named in plain words and lists its own units.
            var states = new[]
            {
                ("Occupied",          "Lived in",        "Someone is renting it and paying for it."),
                ("Available",         "Empty and ready", "Could be rented out tomorrow — nothing is stopping it."),
                ("Reserved",          "Being held",      "Promised to someone who has not moved in yet."),
                ("Under Maintenance", "Being repaired",  "Cannot be rented until the work is finished.")
            };

            vm.UnitStates = states.Select(s => new UnitStateRow
            {
                State = s.Item2,
                Meaning = s.Item3,
                UnitNumbers = units.Where(u => u.Status == s.Item1)
                                   .OrderBy(u => u.UnitNumber)
                                   .Select(u => u.UnitNumber)
                                   .ToList(),
                MonthlyRent = units.Where(u => u.Status == s.Item1).Sum(u => u.RentPrice)
            }).ToList();

            vm.RentBeingEarned = units.Where(u => u.Status == "Occupied").Sum(u => u.RentPrice);
            vm.RentNotBeingEarned = units.Where(u => u.Status != "Occupied").Sum(u => u.RentPrice);

            // ── Tenants ──
            vm.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
            vm.InactiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Inactive");

            // ── Money ──
            var payments = await _context.tblPayments.AsNoTracking().ToListAsync();
            vm.IncomeAllTime = payments.Sum(p => p.Amount);
            vm.IncomeThisMonth = payments.Where(p => p.DatePaid >= firstOfMonth).Sum(p => p.Amount);

            var bills = await _context.tblBillings.AsNoTracking()
                .Include(b => b.Tenant).ThenInclude(t => t!.Unit)
                .ToListAsync();

            // ── Open requests ──
            vm.PendingMaintenance = await _context.tblMaintenanceRequests
                .CountAsync(m => m.Status == "Pending" || m.Status == "In Progress");
            vm.PendingTransfers = await _context.tblUnitTransferRequests.CountAsync(r => r.Status == "Pending");
            vm.OpenLostFound = await _context.tblLostFoundItems.CountAsync(l => l.Status == "Reported");

            // ── Monthly income, last 6 months ──
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = firstOfMonth.AddMonths(-i);
                var billIds = bills.Where(b => b.BillingPeriod == monthStart)
                                   .Select(b => b.BillingID).ToHashSet();
                vm.MonthlyIncome.Add(new MonthlyIncomeRow
                {
                    Month = monthStart,
                    Billed = bills.Where(b => b.BillingPeriod == monthStart).Sum(b => b.AmountDue),
                    // Collected must be measured on the SAME axis as Billed — money
                    // received FOR this month's bills, not money that happened to
                    // arrive during it. Paying a month early put the payment in one
                    // month and the bill it settled in the next, so the row read
                    // 12,000 collected against 6,000 billed and a negative balance.
                    Collected = payments.Where(p => billIds.Contains(p.BillingID)).Sum(p => p.Amount)
                });
            }

            // ── Tenants ──
            var allTenants = await _context.tblTenants.AsNoTracking()
                .Include(t => t.Unit)
                .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
                .ToListAsync();

            // ── Where every tenant's account stands ──
            // Built from the tenant list rather than from the bills, so a tenant
            // who has never been billed still gets a line. A report that lists
            // only the people who owe cannot be used to check the people who
            // don't — and checking is what it is for.
            vm.TenantAccounts = allTenants.Select(t =>
            {
                var theirBills = bills.Where(b => b.TenantID == t.TenantID).ToList();
                var theirBillIds = theirBills.Select(b => b.BillingID).ToHashSet();
                var theirPayments = payments.Where(p => theirBillIds.Contains(p.BillingID))
                                            .OrderByDescending(p => p.DatePaid)
                                            .ToList();
                var last = theirPayments.FirstOrDefault();

                return new TenantBalanceRow
                {
                    TenantID = t.TenantID,
                    TenantName = t.FullName,
                    UnitNumber = t.Unit?.UnitNumber,
                    Status = t.Status,
                    BillsIssued = theirBills.Count,
                    Billed = theirBills.Sum(b => b.AmountDue),
                    Paid = theirBills.Sum(b => b.AmountPaid ?? 0m),
                    Balance = theirBills.Sum(b => b.AmountDue - (b.AmountPaid ?? 0m)),
                    OverdueBills = theirBills.Count(b => b.Status == "Overdue"),
                    AdvanceCredit = t.AdvanceCredit,
                    LastPaymentDate = last?.DatePaid,
                    LastPaymentAmount = last?.Amount ?? 0m
                };
            })
            .OrderByDescending(x => x.Balance).ThenBy(x => x.TenantName)
            .ToList();

            vm.OutstandingTotal = vm.TenantAccounts.Sum(x => x.Balance);
            vm.OverdueTenants = vm.TenantAccounts.Count(x => x.OverdueBills > 0);

                        // ── Maintenance records by status ──
            var requests = await _context.tblMaintenanceRequests.AsNoTracking()
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .ToListAsync();

            vm.MaintenanceByStatus = requests
                .GroupBy(m => m.Status)
                .Select(g => new MaintenanceCountRow { Status = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            // ── Maintenance requests by issue type ──
            vm.MaintenanceByCategory = requests
                .GroupBy(m => m.Category)
                .Select(g => new MaintenanceCategoryRow
                {
                    Category = g.Key,
                    Requests = g.Count()
                })
                .OrderByDescending(x => x.Requests)
                .ToList();

            // ── Tenant histories ──
            var transfers = await _context.tblUnitTransferRequests.AsNoTracking().ToListAsync();

            vm.TenantHistory = allTenants.Select(t =>
            {
                var theirBills = bills.Where(b => b.TenantID == t.TenantID).ToList();
                var theirBillIds = theirBills.Select(b => b.BillingID).ToHashSet();
                var lastPaid = payments.Where(p => theirBillIds.Contains(p.BillingID))
                                       .OrderByDescending(p => p.DatePaid)
                                       .FirstOrDefault();
                return new TenantHistoryRow
                {
                    AdvanceCredit = t.AdvanceCredit,
                    LastPaymentDate = lastPaid?.DatePaid,
                    TenantID = t.TenantID,
                    TenantName = t.FullName,
                    UnitNumber = t.Unit?.UnitNumber,
                    Status = t.Status,
                    MoveInDate = t.MoveInDate,
                    MoveOutDate = t.MoveOutDate,
                    MonthsBilled = theirBills.Count,
                    TotalBilled = theirBills.Sum(b => b.AmountDue),
                    TotalPaid = theirBills.Sum(b => b.AmountPaid ?? 0m),
                    Balance = theirBills.Sum(b => b.AmountDue - (b.AmountPaid ?? 0m)),
                    MaintenanceRequests = requests.Count(m => m.TenantID == t.TenantID),
                    TransferRequests = transfers.Count(r => r.TenantID == t.TenantID)
                };
            }).ToList();

            return View(vm);
        }

        // GET: Reports/MyRecords — a tenant's own read-only record summary
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> MyRecords()
        {
            var tenant = await GetCurrentTenantAsync();
            if (tenant == null) return View(new ReportsViewModel());

            var vm = new ReportsViewModel();

            var bills = await _context.tblBillings.AsNoTracking()
                .Where(b => b.TenantID == tenant.TenantID)
                .ToListAsync();

            var payments = await _context.tblPayments.AsNoTracking()
                .Where(p => p.Billing!.TenantID == tenant.TenantID)
                .ToListAsync();

            vm.IncomeAllTime = payments.Sum(p => p.Amount);            // total this tenant has paid
            vm.OutstandingTotal = bills.Sum(b => b.AmountDue - (b.AmountPaid ?? 0m));
            if (vm.OutstandingTotal < 0) vm.OutstandingTotal = 0;

            vm.PendingMaintenance = await _context.tblMaintenanceRequests
                .CountAsync(m => m.TenantID == tenant.TenantID && (m.Status == "Pending" || m.Status == "In Progress"));
            vm.PendingTransfers = await _context.tblUnitTransferRequests
                .CountAsync(r => r.TenantID == tenant.TenantID && r.Status == "Pending");

            var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = firstOfMonth.AddMonths(-i);
                var billIds = bills.Where(b => b.BillingPeriod == monthStart)
                                   .Select(b => b.BillingID).ToHashSet();
                vm.MonthlyIncome.Add(new MonthlyIncomeRow
                {
                    Month = monthStart,
                    Billed = bills.Where(b => b.BillingPeriod == monthStart).Sum(b => b.AmountDue),
                    // Collected must be measured on the SAME axis as Billed — money
                    // received FOR this month's bills, not money that happened to
                    // arrive during it. Paying a month early put the payment in one
                    // month and the bill it settled in the next, so the row read
                    // 12,000 collected against 6,000 billed and a negative balance.
                    Collected = payments.Where(p => billIds.Contains(p.BillingID)).Sum(p => p.Amount)
                });
            }

            ViewBag.Tenant = tenant;
            return View(vm);
        }
    }
}