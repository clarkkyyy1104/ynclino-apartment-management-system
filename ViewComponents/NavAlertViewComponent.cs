using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using YnclinoApartmentManagementSystem.Services;

namespace YnclinoApartmentManagementSystem.ViewComponents
{
    // Shows a numbered badge on a navigation link. The number comes from
    // SystemNotificationService, which works out what is outstanding by reading
    // the records themselves — so the badge clears when the work is done, not
    // when somebody clicks it.
    public class NavAlertViewComponent : ViewComponent
    {
        private readonly SystemNotificationService _notificationService;

        public NavAlertViewComponent(SystemNotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // "module" may name ONE module ("Billing"), SEVERAL separated by commas
        // ("Billing,Transfer" — used by a dropdown parent so it shows the total of
        // everything inside it), or "All" for everything waiting on this user.
        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            var notifications = await _notificationService.ForCurrentUserAsync(HttpContext.User);

            // each notification says how much it adds, so "3 overdue bills" reads
            // 3 on the badge rather than 1 for the one line it takes in the feed
            int count = module == "All"
                ? notifications.Sum(n => n.BadgeCount)
                : notifications
                    .Where(n => module
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(n.Module, StringComparer.OrdinalIgnoreCase))
                    .Sum(n => n.BadgeCount);

            return View(count);
        }
    }
}
