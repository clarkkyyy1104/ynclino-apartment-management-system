using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CurrentUserIsMainAdmin()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int id)) return false;
            return _context.tblUsers.Any(u => u.UserID == id && u.IsMainAdmin);
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // GET: Users — staff accounts only (Admins)
        public async Task<IActionResult> Index()
        {
            var users = await _context.tblUsers
                .Where(u => u.Role != "Tenant")
                .OrderBy(u => u.Username)
                .ToListAsync();
            ViewBag.IsMainAdmin = CurrentUserIsMainAdmin();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(users);
        }

        // GET: Users/Create — staff accounts only
        public IActionResult Create()
        {
            bool isMainAdmin = CurrentUserIsMainAdmin();
            bool isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                TempData["Error"] = "Only Admins can create staff accounts. Register tenants from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.IsMainAdmin = isMainAdmin;
            ViewBag.IsAdmin = isAdmin;
            var vm = new UserViewModel { Role = "Admin" };
            return View(vm);
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            bool isMainAdmin = CurrentUserIsMainAdmin();
            bool isAdmin = User.IsInRole("Admin");

            // staff accounts are always Admins; tenant accounts come from the Tenants module
            vm.Role = "Admin";

            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required when creating an account.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsMainAdmin = isMainAdmin;
                ViewBag.IsAdmin = isAdmin;
                return View(vm);
            }

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == vm.Username.ToLower());
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsMainAdmin = isMainAdmin;
                ViewBag.IsAdmin = isAdmin;
                return View(vm);
            }

            var user = new tblUser
            {
                Username     = vm.Username,
                Password     = PasswordHelper.Hash(vm.Password!),
                Role         = vm.Role,
                IsActive     = vm.IsActive,
                IsMainAdmin = false,
                MustChangePassword = true,
                DateCreated  = DateTime.Now
            };

            _context.tblUsers.Add(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            bool isMainAdmin = CurrentUserIsMainAdmin();
            bool isAdmin = User.IsInRole("Admin");

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin)
                return Forbid();

            ViewBag.IsMainAdmin = isMainAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.TargetIsMainAdmin = user.IsMainAdmin;
            ViewBag.CanResetPassword = isMainAdmin;
            ViewBag.TargetRole = user.Role;

            return View(new UserViewModel
            {
                UserID   = user.UserID,
                Username = user.Username,
                Role     = user.Role,
                IsActive = user.IsActive
            });
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserViewModel vm)
        {
            if (id != vm.UserID) return NotFound();

            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            bool isMainAdmin = CurrentUserIsMainAdmin();
            bool isAdmin = User.IsInRole("Admin");

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin)
                return Forbid();

            bool isSelf = user.UserID == CurrentUserID();

            // only the main admin can reset another staff account's password
            bool canResetPassword = isMainAdmin;
            if (!canResetPassword || string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            // staff accounts stay Admins; only the username, password, and active flag change here
            vm.Role = "Admin";

            if (user.IsMainAdmin && !vm.IsActive)
                ModelState.AddModelError("IsActive", "The Main Admin account cannot be deactivated.");

            // don't let someone deactivate the account they're signed in with
            if (isSelf && !vm.IsActive)
                ModelState.AddModelError("IsActive", "You cannot deactivate your own account.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsMainAdmin = isMainAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsMainAdmin = user.IsMainAdmin;
                ViewBag.CanResetPassword = canResetPassword;
                ViewBag.TargetRole = user.Role;
                return View(vm);
            }

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == vm.Username.ToLower() && u.UserID != id);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsMainAdmin = isMainAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsMainAdmin = user.IsMainAdmin;
                ViewBag.CanResetPassword = canResetPassword;
                ViewBag.TargetRole = user.Role;
                return View(vm);
            }

            user.Username = vm.Username;
            user.Role     = vm.Role;
            user.IsActive = vm.IsActive;

            if (canResetPassword && !string.IsNullOrWhiteSpace(vm.Password))
                user.Password = PasswordHelper.Hash(vm.Password);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            if (user.IsMainAdmin)
            {
                TempData["Error"] = "The Main Admin account cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!User.IsInRole("Admin"))
                return Forbid();

            if (user.UserID == CurrentUserID())
            {
                TempData["Error"] = "You cannot delete the account you are signed in with.";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            if (user.IsMainAdmin)
            {
                TempData["Error"] = "The Main Admin account cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!User.IsInRole("Admin"))
                return Forbid();

            if (user.UserID == CurrentUserID())
            {
                TempData["Error"] = "You cannot delete the account you are signed in with.";
                return RedirectToAction(nameof(Index));
            }

            // lost & found rows keep a hard reference to their reporter/claimant,
            // so deleting this account would fail at the database level
            bool hasLostFoundRecords =
                await _context.tblLostFoundItems.AnyAsync(l => l.ReportedByUserID == id) ||
                await _context.tblClaimRequests.AnyAsync(c => c.ClaimantUserID == id);
            if (hasLostFoundRecords)
            {
                TempData["Error"] = $"Account '{user.Username}' has Lost & Found records and cannot be deleted. Deactivate it instead.";
                return RedirectToAction(nameof(Index));
            }

            _context.tblUsers.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been deleted.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.tblUsers.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (!user.MustChangePassword)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(user);
        }
    }
}
