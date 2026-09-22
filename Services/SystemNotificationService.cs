using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Services
{
    public class SystemNotificationService
    {
        private readonly ApplicationDbContext _context;

        public SystemNotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Convenience wrapper: works out the user id and the role from the
        // signed-in principal, so callers don't each repeat that. Returns an
        // empty list when nobody is signed in or the role isn't one we serve.
        public Task<List<SystemNotification>> ForCurrentUserAsync(ClaimsPrincipal user)
        {
            if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Task.FromResult(new List<SystemNotification>());

            var role = user.IsInRole("Admin")
                ? "Admin"
                : user.IsInRole("Maintenance")
                    ? "Maintenance"
                    : user.IsInRole("Tenant")
                        ? "Tenant"
                        : string.Empty;

            if (string.IsNullOrEmpty(role))
                return Task.FromResult(new List<SystemNotification>());

            return GetNotificationsAsync(userId, role);
        }

        // Overdue means past the due date and not paid in full — the rule
        // BillingController.DeriveStatus uses. It is read from the dates rather
        // than the stored Status, which is only brought up to date when someone
        // opens the Bills page, so a bill that fell due yesterday is counted today.
        private static Expression<Func<tblBilling, bool>> IsOverdue(DateTime today) =>
            b => (b.AmountPaid == null || b.AmountPaid < b.AmountDue) && b.DueDate < today;

        public async Task<List<SystemNotification>> GetNotificationsAsync(
            int userId,
            string role)
        {
            var notifications = new List<SystemNotification>();
            var today = DateTime.Today;

            // ============================================================
            // ADMIN NOTIFICATIONS
            // ============================================================
            if (role == "Admin")
            {
                // Pending maintenance requests
                var pendingMaintenance =
                    await _context.tblMaintenanceRequests
                        .CountAsync(m => m.Status == "Pending");

                if (pendingMaintenance > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Maintenance",
                        Message = $"{pendingMaintenance} pending maintenance request" +
                                  (pendingMaintenance > 1 ? "s" : "") +
                                  " require attention.",
                        Link = "/Maintenance",
                        CreatedAt = DateTime.Now,
                        BadgeCount = pendingMaintenance
                    });
                }

                // Resolved maintenance requests the admin has not archived yet.
                // In Progress is left out: the staff are on it and nothing waits on
                // the admin. A resolved one tells the admin the work is done, and
                // archiving it is how the admin says they have seen it.
                var resolvedMaintenance =
                    await _context.tblMaintenanceRequests
                        .CountAsync(m => m.Status == "Resolved" && m.StaffArchivedAt == null);

                if (resolvedMaintenance > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Maintenance",
                        Message = $"{resolvedMaintenance} maintenance request" +
                                  (resolvedMaintenance > 1 ? "s have" : " has") +
                                  " been resolved.",
                        Link = "/Maintenance?statusFilter=Resolved",
                        CreatedAt = DateTime.Now,
                        BadgeCount = resolvedMaintenance
                    });
                }

                // Pending unit transfer requests
                var pendingTransfers =
                    await _context.tblUnitTransferRequests
                        .CountAsync(r => r.Status == "Pending");

                if (pendingTransfers > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Transfer",
                        Message = $"{pendingTransfers} pending unit transfer request" +
                                  (pendingTransfers > 1 ? "s" : "") +
                                  " require review.",
                        Link = "/Transfers",
                        CreatedAt = DateTime.Now,
                        BadgeCount = pendingTransfers
                    });
                }

                // Overdue bills — the badge shows how many, not just that there are some
                var overdueBills =
                    await _context.tblBillings
                        .CountAsync(IsOverdue(today));

                if (overdueBills > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Billing",
                        Message = $"{overdueBills} overdue bill" +
                                  (overdueBills > 1 ? "s" : "") +
                                  " require attention.",
                        Link = "/Billing?statusFilter=Overdue",
                        CreatedAt = DateTime.Now,
                        BadgeCount = overdueBills
                    });
                }

                // Claims waiting on a decision.
                //
                // This used to count items with Status 'Reported' — every
                // unclaimed item on the board. Nothing needs doing about an item
                // sitting in the box, so that badge could never reach zero, while
                // the claims that genuinely need a yes or no were not counted at
                // all. A claim is the thing that waits on the admin, so a claim
                // is what the badge counts.
                var pendingClaims =
                    await _context.tblClaimRequests
                        .CountAsync(c => c.Status == "Pending");

                if (pendingClaims > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "LostFound",
                        Message = $"{pendingClaims} claim" +
                                  (pendingClaims > 1 ? "s are" : " is") +
                                  " waiting to be reviewed.",
                        Link = "/LostFound",
                        CreatedAt = DateTime.Now,
                        BadgeCount = pendingClaims
                    });
                }
            }

            // ============================================================
            // MAINTENANCE STAFF NOTIFICATIONS
            // ============================================================
            if (role == "Maintenance")
            {
                var assignedRequests =
                    await _context.tblMaintenanceRequests
                        .CountAsync(m =>
                            m.AssignedStaffID == userId &&
                            (m.Status == "Pending" ||
                             m.Status == "In Progress"));

                if (assignedRequests > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Maintenance",
                        Message = $"{assignedRequests} maintenance request" +
                                  (assignedRequests > 1 ? "s" : "") +
                                  " assigned to you.",
                        Link = "/Maintenance",
                        CreatedAt = DateTime.Now,
                        BadgeCount = assignedRequests
                    });
                }
            }

            // ============================================================
            // TENANT NOTIFICATIONS
            // ============================================================
            if (role == "Tenant")
            {
                var tenant = await _context.tblTenants
                    .FirstOrDefaultAsync(t =>
                        t.UserID == userId &&
                        t.Status == "Active");

                if (tenant != null)
                {
                    // ----------------------------------------------------
                    // BILLING
                    // ----------------------------------------------------
                    var overdueBills =
                        await _context.tblBillings
                            .Where(b => b.TenantID == tenant.TenantID)
                            .CountAsync(IsOverdue(today));

                    if (overdueBills > 0)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Billing",
                            Message = overdueBills == 1
                                ? "You have an overdue bill."
                                : $"You have {overdueBills} overdue bills.",
                            Link = "/Billing",
                            CreatedAt = DateTime.Now,
                            BadgeCount = overdueBills
                        });
                    }

                    // a bill not yet due is worth a line in the feed, but it is
                    // not overdue, so it adds nothing to the Billing badge
                    var unpaidBills =
                        await _context.tblBillings
                            .CountAsync(b =>
                                b.TenantID == tenant.TenantID &&
                                (b.AmountPaid == null || b.AmountPaid < b.AmountDue) &&
                                b.DueDate >= today);

                    if (unpaidBills > 0)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Billing",
                            Message = unpaidBills == 1
                                ? "You have an unpaid or partially paid bill."
                                : $"You have {unpaidBills} unpaid or partially paid bills.",
                            Link = "/Billing",
                            CreatedAt = DateTime.Now,
                            BadgeCount = 0
                        });
                    }

                    // ----------------------------------------------------
                    // MAINTENANCE
                    // ----------------------------------------------------
                    var maintenanceRequests =
                        await _context.tblMaintenanceRequests
                            .Where(m =>
                                m.TenantID == tenant.TenantID &&
                                (m.Status == "Pending" ||
                                 m.Status == "In Progress"))
                            .OrderByDescending(m => m.DateSubmitted)
                            .ToListAsync();

                    foreach (var request in maintenanceRequests)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Maintenance",
                            Message = $"Your {request.Category} maintenance request is {request.Status}.",
                            Link = $"/Maintenance/Details/{request.RequestID}",
                            TargetId = request.RequestID,
                            CreatedAt = request.DateSubmitted
                        });
                    }

                    // ----------------------------------------------------
                    // UNIT TRANSFER
                    // ----------------------------------------------------
                    var transferRequests =
                        await _context.tblUnitTransferRequests
                            .Where(r =>
                                r.TenantID == tenant.TenantID &&
                                r.Status == "Pending")
                            .OrderByDescending(r => r.DateRequested)
                            .ToListAsync();

                    foreach (var request in transferRequests)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Transfer",
                            Message = "Your unit transfer request is still pending.",
                            Link = $"/Transfers/Details/{request.TransferID}",
                            TargetId = request.TransferID,
                            CreatedAt = request.DateRequested
                        });
                    }
                }
            }

            return notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToList();
        }
    }
}