using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // GET: Notifications  — the user's full notification feed (unread shown bold)
        public async Task<IActionResult> Index()
        {
            var uid = CurrentUserID();
            if (uid == null) return View(new List<Models.tblNotification>());

            var list = await _context.tblNotifications
                .Where(n => n.UserID == uid)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync();
            return View(list);
        }

        // GET: Notifications/Open/5  — mark one read, then go where it points
        public async Task<IActionResult> Open(int id)
        {
            var uid = CurrentUserID();
            var note = await _context.tblNotifications.FirstOrDefaultAsync(n => n.NotificationID == id);
            if (note == null || note.UserID != uid) return NotFound();

            if (!note.IsRead)
            {
                note.IsRead = true;
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(note.Link) && Url.IsLocalUrl(note.Link))
                return Redirect(note.Link);
            return RedirectToAction(nameof(Index));
        }

        // POST: Notifications/MarkAllRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var uid = CurrentUserID();
            if (uid != null)
            {
                await _context.tblNotifications
                    .Where(n => n.UserID == uid && !n.IsRead)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
