using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using YnclinoApartmentManagementSystem.Services;

namespace YnclinoApartmentManagementSystem.ViewComponents
{
    public class ModuleNotificationsViewComponent : ViewComponent
    {
        private readonly SystemNotificationService _notificationService;

        public ModuleNotificationsViewComponent(
            SystemNotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string module)
        {
            if (!int.TryParse(
                HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int userId))
            {
                return View(new List<Models.SystemNotification>());
            }

            var role = HttpContext.User.IsInRole("Admin")
                ? "Admin"
                : HttpContext.User.IsInRole("Maintenance")
                    ? "Maintenance"
                    : HttpContext.User.IsInRole("Tenant")
                        ? "Tenant"
                        : string.Empty;

            if (string.IsNullOrEmpty(role))
            {
                return View(new List<Models.SystemNotification>());
            }

            var notifications =
                await _notificationService.GetNotificationsAsync(userId, role);

            var moduleNotifications = notifications
                .Where(n => n.Module.Equals(
                    module,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            return View(moduleNotifications);
        }
    }
}