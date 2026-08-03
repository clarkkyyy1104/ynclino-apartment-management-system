using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Helpers
{
    // small helper for creating and reading per-user notifications.
    // Caller is responsible for having a valid DbContext; each method saves.
    public static class NotificationHelper
    {
        public static async Task CreateAsync(ApplicationDbContext db, int userId, string module, string message, string? link = null, int? targetId = null)
        {
            db.tblNotifications.Add(new tblNotification
            {
                UserID = userId,
                Module = module,
                Message = message,
                Link = link,
                TargetId = targetId,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        // notify every active administrator (e.g. a tenant raised a request)
        public static async Task NotifyAdminsAsync(ApplicationDbContext db, string module, string message, string? link = null, int? targetId = null)
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
                    TargetId = targetId,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

            if (adminIds.Count > 0)
                await db.SaveChangesAsync();
        }

        // ids of the records a user still has UNREAD notifications for, in one module —
        // used to show those rows as unread in the module's list
        public static async Task<HashSet<int>> UnreadTargetIdsAsync(ApplicationDbContext db, int userId, string module)
        {
            var ids = await db.tblNotifications
                .Where(n => n.UserID == userId && n.Module == module && !n.IsRead && n.TargetId != null)
                .Select(n => n.TargetId!.Value)
                .ToListAsync();
            return ids.ToHashSet();
        }

        // ids of records the user has already-READ notifications for (and no unread one),
        // so those rows can be shown greyed
        public static async Task<HashSet<int>> ReadTargetIdsAsync(ApplicationDbContext db, int userId, string module)
        {
            var unread = await db.tblNotifications
                .Where(n => n.UserID == userId && n.Module == module && !n.IsRead && n.TargetId != null)
                .Select(n => n.TargetId!.Value).ToListAsync();
            var read = await db.tblNotifications
                .Where(n => n.UserID == userId && n.Module == module && n.IsRead && n.TargetId != null)
                .Select(n => n.TargetId!.Value).ToListAsync();
            var unreadSet = unread.ToHashSet();
            return read.Where(id => !unreadSet.Contains(id)).ToHashSet();
        }

        // mark the notification(s) for one specific record as read (when the user opens it)
        public static async Task MarkRecordReadAsync(ApplicationDbContext db, int userId, string module, int targetId)
        {
            bool any = await db.tblNotifications.AnyAsync(n =>
                n.UserID == userId && n.Module == module && n.TargetId == targetId && !n.IsRead);
            if (!any) return;

            await db.tblNotifications
                .Where(n => n.UserID == userId && n.Module == module && n.TargetId == targetId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }
    }
}
