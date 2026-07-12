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
    [Authorize(Roles = "Admin,SemiAdmin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CurrentUserIsSuperAdmin()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int id)) return false;
            return _context.tblUsers.Any(u => u.UserID == id && u.IsSuperAdmin);
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // GET: Users — staff accounts only (Admin / SemiAdmin)
        public async Task<IActionResult> Index()
        {
            var users = await _context.tblUsers
                .Where(u => u.Role != "Tenant")
                .OrderBy(u => u.Username)
                .ToListAsync();
            ViewBag.IsSuperAdmin = CurrentUserIsSuperAdmin();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(users);
        }

        // GET: Users/Create — staff accounts only
        public IActionResult Create()
        {
            bool isSuperAdmin = CurrentUserIsSuperAdmin();
            bool isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                TempData["Error"] = "Only Admins can create staff accounts. Register tenants from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            var vm = new UserViewModel { Role = "SemiAdmin" };
            return View(vm);
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            bool isSuperAdmin = CurrentUserIsSuperAdmin();
            bool isAdmin = User.IsInRole("Admin");

            if (vm.Role == "Tenant")
                ModelState.AddModelError("Role", "Tenant accounts must be created from the Tenants module.");

            if (!isSuperAdmin && vm.Role == "Admin")
                ModelState.AddModelError("Role", "Only the Super Admin can create Admin accounts.");

            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required when creating an account.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                return View(vm);
            }

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == vm.Username.ToLower());
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                return View(vm);
            }

            var user = new tblUser
            {
                Username     = vm.Username,
                Password     = PasswordHelper.Hash(vm.Password!),
                Role         = vm.Role,
                IsActive     = vm.IsActive,
                IsSuperAdmin = false,
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

            bool isSuperAdmin = CurrentUserIsSuperAdmin();
            bool isAdmin = User.IsInRole("Admin");

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin)
                return Forbid();

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;
            ViewBag.CanResetPassword = isSuperAdmin || (isAdmin && user.Role == "SemiAdmin");
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

            bool isSuperAdmin = CurrentUserIsSuperAdmin();
            bool isAdmin = User.IsInRole("Admin");

            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin)
                return Forbid();

            bool isSelf = user.UserID == CurrentUserID();

            // super admin can reset any staff password, a regular admin only semi-admin ones
            bool canResetPassword = isSuperAdmin || (isAdmin && user.Role == "SemiAdmin");
            if (!canResetPassword || string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            // promoting to Admin is super-admin territory; keeping an existing Admin role is fine
            if (!isSuperAdmin && vm.Role == "Admin" && user.Role != "Admin")
                ModelState.AddModelError("Role", "Only the Super Admin can assign the Admin role.");

            if (vm.Role == "Tenant")
                ModelState.AddModelError("Role", "Use the Tenants module to manage Tenant accounts.");

            if (user.IsSuperAdmin)
            {
                if (vm.Role != "Admin")
                    ModelState.AddModelError("Role", "The Super Admin role cannot be changed.");
                if (!vm.IsActive)
                    ModelState.AddModelError("IsActive", "The Super Admin account cannot be deactivated.");
            }

            // don't let someone demote or deactivate the account they're signed in with
            if (isSelf && vm.Role != user.Role)
                ModelState.AddModelError("Role", "You cannot change the role of your own account.");
            if (isSelf && !vm.IsActive)
                ModelState.AddModelError("IsActive", "You cannot deactivate your own account.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;
                ViewBag.CanResetPassword = canResetPassword;
                ViewBag.TargetRole = user.Role;
                return View(vm);
            }

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == vm.Username.ToLower() && u.UserID != id);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;
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

            if (user.IsSuperAdmin)
            {
                TempData["Error"] = "The Super Admin account cannot be deleted.";
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

            if (user.IsSuperAdmin)
            {
                TempData["Error"] = "The Super Admin account cannot be deleted.";
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
    }
}
