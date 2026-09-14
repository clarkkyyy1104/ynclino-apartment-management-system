using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace YnclinoApartmentManagementSystem.Filters
{
    // Keeps a user whose account still carries the "MustChangePassword" claim locked
    // on the mandatory password-change page until they set their own password. Every
    // other action redirects there; the change page itself and Logout are allowed.
    public class MustChangePasswordFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            // admins are never forced — only a signed-in tenant carrying the claim is locked
            if (user?.Identity?.IsAuthenticated == true
                && !user.IsInRole("Admin")
                && user.HasClaim("MustChangePassword", "true"))
            {
                var controller = context.RouteData.Values["controller"] as string;
                var action = context.RouteData.Values["action"] as string;

                bool allowed = controller == "Account"
                    && (action == "MandatoryPassChange" || action == "Logout");

                if (!allowed)
                {
                    context.Result = new RedirectToActionResult("MandatoryPassChange", "Account", null);
                    return;
                }
            }

            await next();
        }
    }
}
