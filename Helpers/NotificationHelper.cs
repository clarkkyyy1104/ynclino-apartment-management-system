using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Helpers
{
    // small helper for creating and reading per-user notifications.
    // Caller is responsible for having a valid DbContext; each method saves.
    public static class NotificationHelper
    {
        public static async Task CreateAsync(ApplicationDbContext db, int userId, string module, string message, string? link = null)
        {
            db.tblNotifications.Add(new tblNotification
            {
                UserID = userId,
                Module = module,
                Message = message,
                Link = link,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        // notify every active administrator (e.g. a tenant raised a request)
        public static async Task NotifyAdminsAsync(ApplicationDbContext db, string module, string message, string? link = null)
        {
            var adminIds = await db.tblUsers
                .Where(u => u.Role == "Admin" && u.IsActive)
                .Select(u => u.UserID)
                .ToListAsync();

            foreach (var id in adminIds)
                db.tblNotifications.Add(new tblNotification
                {
                    UserID = id,
                    Module = module,
                    Message = message,
                    Link = link,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

            if (adminIds.Count > 0)
                await db.SaveChangesAsync();
        }

        // mark a user's notifications for one module as read (called when they open it)
        public static async Task MarkModuleReadAsync(ApplicationDbContext db, int userId, string module)
        {
            bool any = await db.tblNotifications.AnyAsync(n => n.UserID == userId && n.Module == module && !n.IsRead);
            if (!any) return;

            await db.tblNotifications
                .Where(n => n.UserID == userId && n.Module == module && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }
    }
}
