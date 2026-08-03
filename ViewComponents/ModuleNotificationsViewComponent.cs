using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.ViewComponents
{
    // Shows the current user's UNREAD notifications for one module as a small
    // panel at the top of that module's page. Each message links to
    // Notifications/Open which marks only that one read — so the module's badge
    // count persists until the user actually clicks the notification.
    public class ModuleNotificationsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public ModuleNotificationsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            var list = new List<tblNotification>();
            if (int.TryParse(((ClaimsPrincipal)User).FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
            {
                list = await _context.tblNotifications
                    .Where(n => n.UserID == uid && n.Module == module && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            return View(list);
        }
    }
}
