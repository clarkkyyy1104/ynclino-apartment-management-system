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

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.tblUsers.OrderBy(u => u.Username).ToListAsync();
            ViewBag.IsSuperAdmin = CurrentUserIsSuperAdmin();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(users);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            ViewBag.IsSuperAdmin = CurrentUserIsSuperAdmin();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(new UserViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            bool isSuperAdmin = CurrentUserIsSuperAdmin();
            bool isAdmin = User.IsInRole("Admin");

            // SemiAdmin can only create Tenant accounts
            if (!isAdmin && vm.Role != "Tenant")
            {
                ModelState.AddModelError("Role", "You can only create Tenant accounts.");
            }
            // Only Super Admin can create Admin accounts
            if (isAdmin && !isSuperAdmin && vm.Role == "Admin")
            {
                ModelState.AddModelError("Role", "Only the Super Admin can create Admin accounts.");
            }

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
                Username  = vm.Username,
                Password  = PasswordHelper.Hash(vm.Password!),
                Role      = vm.Role,
                IsActive  = vm.IsActive,
                IsSuperAdmin = false,
                DateCreated = DateTime.Now
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

            // SemiAdmin cannot edit Admin or SemiAdmin accounts
            if (!isAdmin && user.Role != "Tenant")
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

            // SemiAdmin cannot edit Admin or SemiAdmin accounts
            if (!isAdmin && user.Role != "Tenant")
                return Forbid();

            // Remove password validation if field left blank
            if (string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            // Only Super Admin can assign Admin role
            if (isAdmin && !isSuperAdmin && vm.Role == "Admin")
                ModelState.AddModelError("Role", "Only the Super Admin can assign the Admin role.");

            // SemiAdmin cannot change role away from Tenant
            if (!isAdmin && vm.Role != "Tenant")
                ModelState.AddModelError("Role", "You can only manage Tenant accounts.");

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

            // Super admin is undeletable
            if (user.IsSuperAdmin)
            {
                TempData["Error"] = "The Super Admin account cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            bool isAdmin = User.IsInRole("Admin");

            // SemiAdmin can only delete Tenant accounts
            if (!isAdmin && user.Role != "Tenant")
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

            bool isAdmin = User.IsInRole("Admin");
            if (!isAdmin && user.Role != "Tenant")
                return Forbid();

            _context.tblUsers.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
