using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Helpers;
using YnclinoAMS.Models;
using YnclinoAMS.Models.ViewModels;

namespace YnclinoAMS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.tblUsers.OrderBy(u => u.Username).ToListAsync();
            return View(users);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View(new UserViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required when creating an account.");

            if (!ModelState.IsValid)
                return View(vm);

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                return View(vm);
            }

            var user = new tblUser
            {
                Username = vm.Username,
                Password = PasswordHelper.Hash(vm.Password!),
                Role = vm.Role,
                IsActive = vm.IsActive,
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

            var vm = new UserViewModel
            {
                UserID = user.UserID,
                Username = user.Username,
                Role = user.Role,
                IsActive = user.IsActive
            };
            return View(vm);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserViewModel vm)
        {
            if (id != vm.UserID) return NotFound();

            // Password fields are optional on edit — clear compare error if password is blank
            if (string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            if (!ModelState.IsValid)
                return View(vm);

            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username && u.UserID != id);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                return View(vm);
            }

            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            // Prevent deactivating the last admin
            if (user.Role == "Admin" && vm.Role != "Admin")
            {
                int adminCount = await _context.tblUsers.CountAsync(u => u.Role == "Admin" && u.IsActive);
                if (adminCount <= 1)
                {
                    ModelState.AddModelError(string.Empty, "Cannot remove the last active Admin account.");
                    return View(vm);
                }
            }

            user.Username = vm.Username;
            user.Role = vm.Role;
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
            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.tblUsers.FindAsync(id);
            if (user == null) return NotFound();

            // Prevent deleting the last admin
            if (user.Role == "Admin")
            {
                int adminCount = await _context.tblUsers.CountAsync(u => u.Role == "Admin" && u.IsActive);
                if (adminCount <= 1)
                {
                    TempData["Error"] = "Cannot delete the last active Admin account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            _context.tblUsers.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
