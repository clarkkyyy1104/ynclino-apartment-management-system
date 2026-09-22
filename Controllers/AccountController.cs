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

        // ── Sign-in pages ──────────────────────────────────────────────────
        // Three pages, one for each kind of account. The tenant page is the
        // default: it keeps the /Account/Login address, which is where the
        // cookie sends anyone who is not signed in. Staff reach theirs from the
        // links under the form, or directly at /Account/MaintenanceLogin and
        // /Account/AdminLogin. There is no self-service reset.

        [HttpGet]
        public IActionResult Login(string? returnUrl) => ShowPortal(LoginPortal.Tenant, returnUrl);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Login(LoginViewModel vm, string? returnUrl) =>
            SignInThroughAsync(LoginPortal.Tenant, vm, returnUrl);

        [HttpGet]
        public IActionResult MaintenanceLogin(string? returnUrl) => ShowPortal(LoginPortal.Maintenance, returnUrl);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> MaintenanceLogin(LoginViewModel vm, string? returnUrl) =>
            SignInThroughAsync(LoginPortal.Maintenance, vm, returnUrl);

        [HttpGet]
        public IActionResult AdminLogin(string? returnUrl) => ShowPortal(LoginPortal.Admin, returnUrl);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> AdminLogin(LoginViewModel vm, string? returnUrl) =>
            SignInThroughAsync(LoginPortal.Admin, vm, returnUrl);

        private IActionResult ShowPortal(LoginPortal portal, string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return PortalView(portal, new LoginViewModel(), returnUrl);
        }

        // every sign-in page renders the same view; only the portal changes
        private IActionResult PortalView(LoginPortal portal, LoginViewModel vm, string? returnUrl)
        {
            ViewBag.Portal = portal;
            ViewBag.ReturnUrl = returnUrl;
            return View("Login", vm);
        }

        private async Task<IActionResult> SignInThroughAsync(LoginPortal portal, LoginViewModel vm, string? returnUrl)
        {
            if (!ModelState.IsValid)
                return PortalView(portal, vm, returnUrl);

            var username = vm.Username.Trim();

            // stop an online guessing run before it starts
            var wait = LoginThrottle.RetryAfter(username);
            if (wait != null)
            {
                ModelState.AddModelError(string.Empty,
                    $"Too many failed sign-in attempts. Try again in {Math.Ceiling(wait.Value.TotalMinutes)} minute(s).");
                return PortalView(portal, vm, returnUrl);
            }

            var user = await _context.tblUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null || !PasswordHelper.Verify(vm.Password, user.Password))
            {
                LoginThrottle.RecordFailure(username);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return PortalView(portal, vm, returnUrl);
            }

            LoginThrottle.RecordSuccess(username);

            // correct password on a deactivated account gets a clearer message
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the administrator.");
                return PortalView(portal, vm, returnUrl);
            }

            // Right password, wrong page. Nobody is signed in; they are pointed at
            // the page their account belongs to. Saying which kind of account it is
            // gives nothing away — the password has already been proven correct.
            if (user.Role != portal.Role)
            {
                ViewBag.RightPortal = LoginPortal.For(user.Role);
                ModelState.AddModelError(string.Empty, $"This page is for {portal.Audience} accounts only.");
                return PortalView(portal, vm, returnUrl);
            }

            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await SignInUserAsync(user);

            // a forced password change takes priority over everything else. It applies
            // to every account an admin creates for someone else — tenants and
            // maintenance staff — but never to admins themselves.
            if (user.MustChangePassword && user.Role != "Admin")
                return RedirectToAction(nameof(MandatoryPassChange));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // issues the auth cookie for a user whose password has already been verified
        private async Task SignInUserAsync(tblUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // carry the "must change password" state in the cookie so it can be
            // enforced on every request without hitting the database each time.
            // This applies to tenants and maintenance staff — admins are never forced.
            if (user.MustChangePassword && user.Role != "Admin")
                claims.Add(new Claim("MustChangePassword", "true"));

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
            // send each person back to their own sign-in page, not the tenant default
            var portal = LoginPortal.For(User.FindFirstValue(ClaimTypes.Role));

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(portal.Action);
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

        // A tenant/user whose account was created by an admin is forced through this
        // page on first login and cannot use the rest of the app until they set their
        // own password (enforced globally by MustChangePasswordFilter).
        [Authorize]
        [HttpGet]
        public IActionResult MandatoryPassChange() => View(new ChangePasswordViewModel());

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MandatoryPassChange(ChangePasswordViewModel vm)
        {
            // the current password isn't asked for here — the user just logged in with it
            ModelState.Remove(nameof(vm.CurrentPassword));
            if (!ModelState.IsValid)
                return View(vm);

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int userId))
                return Forbid();

            var user = await _context.tblUsers.FindAsync(userId);
            if (user == null)
                return Forbid();

            if (PasswordHelper.Verify(vm.NewPassword, user.Password))
            {
                ModelState.AddModelError("NewPassword", "Please choose a new password, different from your temporary one.");
                return View(vm);
            }

            user.Password = PasswordHelper.Hash(vm.NewPassword);
            user.MustChangePassword = false;
            await _context.SaveChangesAsync();

            // re-issue the cookie so the "must change" claim is dropped
            await SignInUserAsync(user);

            TempData["Success"] = "Your password has been set. Welcome!";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
