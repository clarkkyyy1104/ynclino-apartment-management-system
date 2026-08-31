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

                // "module" may name ONE module ("Billing"), SEVERAL separated by commas
        // ("Billing,Transfer" — used by a dropdown parent so it shows the total of
        // everything inside it), or "All" for the user's whole unread count.
        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            int uid = CurrentUserId() ?? 0;
            int count = 0;
            if (uid != 0)
            {
                var q = _context.tblNotifications.Where(n => n.UserID == uid && !n.IsRead);

                if (module != "All")
                {
                    var modules = module.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    q = q.Where(n => modules.Contains(n.Module));
                }

                count = await q.CountAsync();
            }
            return View(count);
        }

        private int? CurrentUserId() =>
            int.TryParse(((ClaimsPrincipal)User).FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
    }
}
