using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Helpers;
using YnclinoAMS.Models;
using YnclinoAMS.Models.ViewModels;

namespace YnclinoAMS.Controllers
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
            // SemiAdmin has no reason to be here (they can't create staff); redirect them out
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

            // Staff accounts only — Tenants are registered through the Tenants module
            if (vm.Role == "Tenant")
                ModelState.AddModelError("Role", "Tenant accounts must be created from the Tenants module.");

            // Only Super Admin can create Admin accounts
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

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username);
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

            // Tenant accounts are managed from Tenants module
            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            // SemiAdmin cannot edit Admin or SemiAdmin accounts
            if (!isAdmin)
                return Forbid();

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;

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

            // Tenant accounts are managed from Tenants module
            if (user.Role == "Tenant")
            {
                TempData["Error"] = "Tenant accounts are managed from the Tenants module.";
                return RedirectToAction(nameof(Index));
            }

            // Only Admins can edit staff accounts
            if (!isAdmin)
                return Forbid();

            // Remove password validation if field left blank
            if (string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            // Only Super Admin can assign Admin role
            if (!isSuperAdmin && vm.Role == "Admin")
                ModelState.AddModelError("Role", "Only the Super Admin can assign the Admin role.");

            // Prevent assigning Tenant role from here
            if (vm.Role == "Tenant")
                ModelState.AddModelError("Role", "Use the Tenants module to manage Tenant accounts.");

            // Cannot demote or deactivate the super admin
            if (user.IsSuperAdmin)
            {
                if (vm.Role != "Admin")
                    ModelState.AddModelError("Role", "The Super Admin role cannot be changed.");
                if (!vm.IsActive)
                    ModelState.AddModelError("IsActive", "The Super Admin account cannot be deactivated.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;
                return View(vm);
            }

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username && u.UserID != id);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsSuperAdmin = isSuperAdmin;
                ViewBag.IsAdmin = isAdmin;
                ViewBag.TargetIsSuperAdmin = user.IsSuperAdmin;
                return View(vm);
            }

            user.Username = vm.Username;
            user.Role     = vm.Role;
            user.IsActive = vm.IsActive;

            if (!string.IsNullOrWhiteSpace(vm.Password))
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

            _context.tblUsers.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
