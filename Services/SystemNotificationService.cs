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

        public async Task<List<SystemNotification>> GetNotificationsAsync(
            int userId,
            string role)
        {
            var notifications = new List<SystemNotification>();

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
                        CreatedAt = DateTime.Now
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
                        CreatedAt = DateTime.Now
                    });
                }

                // Overdue bills
                var overdueBills =
                    await _context.tblBillings
                        .CountAsync(b => b.Status == "Overdue");

                if (overdueBills > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "Billing",
                        Message = $"{overdueBills} overdue bill" +
                                  (overdueBills > 1 ? "s" : "") +
                                  " require attention.",
                        Link = "/Billing?statusFilter=Overdue",
                        CreatedAt = DateTime.Now
                    });
                }

                // Reported lost/found items
                var reportedItems =
                    await _context.tblLostFoundItems
                        .CountAsync(i => i.Status == "Reported");

                if (reportedItems > 0)
                {
                    notifications.Add(new SystemNotification
                    {
                        Module = "LostFound",
                        Message = $"{reportedItems} lost/found item" +
                                  (reportedItems > 1 ? "s" : "") +
                                  " require attention.",
                        Link = "/LostFound",
                        CreatedAt = DateTime.Now
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
                        CreatedAt = DateTime.Now
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
                            .CountAsync(b =>
                                b.TenantID == tenant.TenantID &&
                                b.Status == "Overdue");

                    if (overdueBills > 0)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Billing",
                            Message = overdueBills == 1
                                ? "You have an overdue bill."
                                : $"You have {overdueBills} overdue bills.",
                            Link = "/Billing",
                            CreatedAt = DateTime.Now
                        });
                    }

                    var unpaidBills =
                        await _context.tblBillings
                            .CountAsync(b =>
                                b.TenantID == tenant.TenantID &&
                                (b.Status == "Unpaid" ||
                                 b.Status == "Partial"));

                    if (unpaidBills > 0)
                    {
                        notifications.Add(new SystemNotification
                        {
                            Module = "Billing",
                            Message = unpaidBills == 1
                                ? "You have an unpaid or partially paid bill."
                                : $"You have {unpaidBills} unpaid or partially paid bills.",
                            Link = "/Billing",
                            CreatedAt = DateTime.Now
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