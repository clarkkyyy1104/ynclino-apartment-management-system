using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // the unread notifications for whoever is signed in — every dashboard shows
        // the same feed, so they all read from here
        private async Task<List<tblNotification>> MyUnreadAsync()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
                return new List<tblNotification>();

            return await _context.tblNotifications
                .Where(n => n.UserID == uid && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToListAsync();
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin"))
            {
                ViewBag.TotalUnits = await _context.tblUnits.CountAsync();
                ViewBag.AvailableUnits = await _context.tblUnits.CountAsync(u => u.Status == "Available");
                ViewBag.ReservedUnits = await _context.tblUnits.CountAsync(u => u.Status == "Reserved");
                ViewBag.OccupiedUnits = await _context.tblUnits.CountAsync(u => u.Status == "Occupied");
                ViewBag.MaintenanceUnits = await _context.tblUnits.CountAsync(u => u.Status == "Under Maintenance");
                ViewBag.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
                ViewBag.InactiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Inactive");
                ViewBag.TotalUsers = await _context.tblUsers.CountAsync(u => u.IsActive);
                ViewBag.Unread = await MyUnreadAsync();
                return View("AdminDashboard");
            }
            else if (User.IsInRole("Maintenance"))
            {
                // maintenance staff get their own dashboard: only their assigned work
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int staffId);

                var mine = await _context.tblMaintenanceRequests
                    .Include(m => m.Tenant)
                    .Include(m => m.Unit)
                    .Where(m => m.AssignedStaffID == staffId)
                    .ToListAsync();

                var vm = new StaffDashboardViewModel
                {
                    StaffName = User.Identity?.Name ?? "Maintenance Staff",
                    MyOpenRequests = mine
                        .Where(m => m.Status == "Pending" || m.Status == "In Progress")
                        .OrderByDescending(m => m.Priority == "Urgent")
                        .ThenByDescending(m => m.DateSubmitted)
                        .ToList(),
                    PendingCount = mine.Count(m => m.Status == "Pending"),
                    InProgressCount = mine.Count(m => m.Status == "In Progress"),
                    ResolvedCount = mine.Count(m => m.Status == "Resolved")
                };
                vm.UrgentCount = vm.MyOpenRequests.Count(m => m.Priority == "Urgent");
                ViewBag.Unread = await MyUnreadAsync();

                return View("StaffDashboard", vm);
            }
            else
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var vm = new TenantDashboardViewModel();

                if (int.TryParse(userIdStr, out int userId))
                {
                    vm.Tenant = await _context.tblTenants
                        .Include(t => t.Unit)
                        .FirstOrDefaultAsync(t => t.UserID == userId && t.Status == "Active");

                    // Only UNREAD notifications reach the dashboard. Opening one marks
                    // it read, so it disappears from here on the next page load.
                    vm.Unread = await _context.tblNotifications
                        .Where(n => n.UserID == userId && !n.IsRead)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(20)
                        .ToListAsync();

                    vm.BillingCount     = vm.Unread.Count(n => n.Module == "Billing");
                    vm.MaintenanceCount = vm.Unread.Count(n => n.Module == "Maintenance");
                    vm.LostFoundCount   = vm.Unread.Count(n => n.Module == "LostFound");
                    vm.TransferCount    = vm.Unread.Count(n => n.Module == "Transfer");

                    if (vm.Tenant != null)
                    {
                        vm.AdvanceCredit = vm.Tenant.AdvanceCredit;
                        vm.Outstanding = await _context.tblBillings
                            .Where(b => b.TenantID == vm.Tenant.TenantID && b.Status != "Paid")
                            .SumAsync(b => b.AmountDue - (b.AmountPaid ?? 0m));
                    }
                }
                return View("TenantDashboard", vm);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
