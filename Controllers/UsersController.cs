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

        // GET: Users — EVERY account the system issues a login to. The manuscript
        // scopes User Account Management to "Admin/Owner, Administrators, Tenants,
        // and Maintenance Staff", so all four appear here.
        //
        // Include(Tenants) so a tenant row can show the person's real name instead of
        // their login, and so we can link straight to their tenant record.
        public async Task<IActionResult> Index()
        {
            var users = await _context.tblUsers
                .Include(u => u.Tenants)
                .ToListAsync();

            // Admins first, then maintenance staff, then tenants — the order an admin
            // thinks in, rather than alphabetical across three different kinds of account
            ViewBag.Accounts = users
                .OrderBy(u => u.Role switch { "Admin" => 0, "Maintenance" => 1, _ => 2 })
                .ThenByDescending(u => u.IsMainAdmin)
                .ThenBy(u => u.DisplayName)
                .ToList();

            ViewBag.AdminCount = users.Count(u => u.Role == "Admin");
            ViewBag.StaffCount = users.Count(u => u.Role == "Maintenance");
            ViewBag.TenantCount = users.Count(u => u.Role == "Tenant");

            ViewBag.IsMainAdmin = CurrentUserIsMainAdmin();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View((ViewBag.Accounts as List<tblUser>)!);
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

        // the two staff roles this module can create; "Tenant" is deliberately absent
        private static readonly string[] StaffRoles = { "Admin", "Maintenance" };

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            bool isMainAdmin = CurrentUserIsMainAdmin();
            bool isAdmin = User.IsInRole("Admin");

            // this module creates staff accounts only — Admin or Maintenance. Anything
            // else (a forged "Tenant", say) is REFUSED, never quietly turned into an
            // Admin: silently granting more power than was asked for is the worst
            // possible default.
            if (!StaffRoles.Contains(vm.Role))
                ModelState.AddModelError("Role", "Choose Administrator or Maintenance Staff. Tenant accounts are created from the Tenants module.");

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
                // an account created FOR someone else must have its password changed on
                // first login. Admins set their own password, so they are never forced.
                MustChangePassword = vm.Role != "Admin",
                DateCreated  = DateTime.Now
            };

            _context.tblUsers.Add(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Account '{user.Username}' has been created.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Users/ToggleActive/5 — switch an account on or off.
        //
        // For a TENANT the two states move together: the login is switched and the
        // tenancy follows it, so deactivating here has exactly the same effect as
        // archiving them from the Tenants module. Move-out date and unit occupancy
        // are kept in step, the same way TenantsController does it — otherwise a
        // deactivated tenant would still be holding their apartment.
        //
        // For an ADMIN or MAINTENANCE account there is no tenancy, so only the
        // login changes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.tblUsers.Include(u => u.Tenants).FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound();

            // the Main Admin must always be able to get back in
            if (user.IsMainAdmin)
            {
                TempData["Error"] = "The Main Admin account cannot be deactivated.";
                return RedirectToAction(nameof(Index));
            }

            // locking yourself out of the system you administer
            if (user.UserID == CurrentUserID())
            {
                TempData["Error"] = "You cannot deactivate the account you are signed in with.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            bool activating = user.IsActive;

            // a tenant's record follows their account
            var tenant = user.Tenants?.FirstOrDefault();
            if (tenant != null)
            {
                tenant.Status = activating ? "Active" : "Inactive";

                // an active tenant has no move-out date; a deactivated one is stamped
                // with today unless a date was already recorded
                tenant.MoveOutDate = activating ? null : (tenant.MoveOutDate ?? DateTime.Now);
            }

            await _context.SaveChangesAsync();

            // the unit's Available/Occupied state is derived from how many ACTIVE
            // tenants it holds, so it has to be recomputed after the change
            if (tenant?.UnitID != null)
            {
                await UnitStatusHelper.RefreshAsync(_context, tenant.UnitID.Value);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = activating
                ? $"{user.DisplayName} can sign in again" + (tenant != null ? " and their tenancy is Active." : ".")
                : $"{user.DisplayName} has been deactivated" + (tenant != null ? " and their tenancy is now Inactive." : ".");

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
                await _context.tblLostFoundItems.AnyAsync(l => l.ClaimedByUserID == id) ||
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
