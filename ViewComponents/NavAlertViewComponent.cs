using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.ViewComponents
{
    // Shows a red numbered badge on a navigation link with the current user's
    // count of UNREAD notifications for that module. Rendered from _Layout.
    public class NavAlertViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NavAlertViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            int uid = CurrentUserId() ?? 0;
            int count = 0;
            if (uid != 0)
            {
                var q = _context.tblNotifications.Where(n => n.UserID == uid && !n.IsRead);
                if (module != "All") q = q.Where(n => n.Module == module);   // "All" = total unread
                count = await q.CountAsync();
            }
            return View(count);
        }

        private int? CurrentUserId() =>
            int.TryParse(((ClaimsPrincipal)User).FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
    }
}
