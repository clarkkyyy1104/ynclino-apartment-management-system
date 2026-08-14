using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(vm);
            }

            var username = vm.Username.Trim();
            var user = await _context.tblUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null || !PasswordHelper.Verify(vm.Password, user.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                ViewBag.ReturnUrl = returnUrl;
                return View(vm);
            }

            // correct password on a deactivated account gets a clearer message
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the administrator.");
                ViewBag.ReturnUrl = returnUrl;
                return View(vm);
            }

            await SignInUserAsync(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (user.MustChangePassword)
            {
                return RedirectToAction("MandatoryPassChange", new { id = user.UserID });
            }

            return RedirectToAction("Index", "Home");
        }

        // Demo/presentation convenience: sign in as a sample admin or tenant with one
        // click (no password), so the system can be shown/tested without typing logins.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickLogin(string role)
        {
            tblUser? user = null;
            if (role == "Admin")
            {
                user = await _context.tblUsers
                    .Where(u => u.Role == "Admin" && u.IsActive)
                    .OrderByDescending(u => u.IsMainAdmin)
                    .FirstOrDefaultAsync();
            }
            else if (role == "Tenant")
            {
                // prefer a tenant that already has a unit, so the demo shows real data
                var tenant = await _context.tblTenants
                    .Include(t => t.User)
                    .Where(t => t.User!.IsActive && t.User.Role == "Tenant" && t.Status == "Active")
                    .OrderByDescending(t => t.UnitID != null)
                    .ThenBy(t => t.TenantID)
                    .FirstOrDefaultAsync();
                user = tenant?.User;
            }

            if (user == null)
            {
                TempData["Error"] = role == "Tenant"
                    ? "No sample tenant found. Ask the admin to load sample data first."
                    : "No administrator account found.";
                return RedirectToAction(nameof(Login));
            }

            await SignInUserAsync(user);
            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUserAsync(tblUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // non-persistent so it dies when the browser closes
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int userId))
                return Forbid();

            var user = await _context.tblUsers.FindAsync(userId);
            if (user == null)
                return Forbid();

            if (!PasswordHelper.Verify(vm.CurrentPassword, user.Password))
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(vm);
            }

            if (vm.NewPassword == vm.CurrentPassword)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from the current one.");
                return View(vm);
            }

            user.Password = PasswordHelper.Hash(vm.NewPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
