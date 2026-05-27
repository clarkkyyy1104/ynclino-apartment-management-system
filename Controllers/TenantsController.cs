using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Helpers;
using YnclinoAMS.Models;
using YnclinoAMS.Models.ViewModels;

namespace YnclinoAMS.Controllers
{
    [Authorize]
    public class TenantsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TenantsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CurrentUserIsSuperAdmin()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int id)) return false;
            return _context.tblUsers.Any(u => u.UserID == id && u.IsSuperAdmin);
        }

        // GET: Tenants
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            var query = _context.tblTenants.Include(t => t.Unit).AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(t => t.Status == statusFilter);
            else
                query = query.Where(t => t.Status == "Active");

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(t => t.FirstName.Contains(searchTerm) || t.LastName.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter ?? "Active";
            ViewBag.SearchTerm = searchTerm;

            var tenants = await query.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToListAsync();
            return View(tenants);
        }

        // GET: Tenants/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TenantID == id);

            if (tenant == null) return NotFound();
            return View(tenant);
        }

        // GET: Tenants/Create
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Create()
        {
            var vm = new TenantViewModel
            {
                AvailableUnits = await GetAvailableUnitsAsync()
            };
            return View(vm);
        }

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Create(TenantViewModel vm)
        {
            // Account fields are required on create
            if (string.IsNullOrWhiteSpace(vm.Username))
                ModelState.AddModelError("Username", "Username is required.");
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required.");

            if (!ModelState.IsValid)
            {
                vm.AvailableUnits = await GetAvailableUnitsAsync();
                return View(vm);
            }

            // Check username uniqueness
            bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username);
            if (duplicate)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                vm.AvailableUnits = await GetAvailableUnitsAsync();
                return View(vm);
            }

            // Create login account first (need the generated UserID)
            var user = new tblUser
            {
                Username    = vm.Username!,
                Password    = PasswordHelper.Hash(vm.Password!),
                Role        = "Tenant",
                IsActive    = true,
                IsSuperAdmin = false,
                DateCreated = DateTime.Now
            };
            _context.tblUsers.Add(user);
            await _context.SaveChangesAsync();

            // Create tenant profile linked to the new account
            var tenant = new tblTenant
            {
                UserID           = user.UserID,
                UnitID           = vm.UnitID,
                FirstName        = vm.FirstName,
                LastName         = vm.LastName,
                ContactNumber    = vm.ContactNumber,
                EmergencyContact = vm.EmergencyContact,
                MoveInDate       = vm.MoveInDate,
                MoveOutDate      = vm.MoveOutDate,
                LeaseStart       = vm.LeaseStart,
                LeaseEnd         = vm.LeaseEnd,
                Status           = "Active",
                DateRecorded     = DateTime.Now
            };
            _context.tblTenants.Add(tenant);

            var unit = await _context.tblUnits.FindAsync(vm.UnitID);
            if (unit != null) unit.Status = "Occupied";

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Tenant {tenant.FullName} has been registered with account '{user.Username}'.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Edit/5
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // Load linked account username if one exists
            tblUser? linkedUser = null;
            if (tenant.UserID.HasValue)
                linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);

            ViewBag.IsSuperAdmin = CurrentUserIsSuperAdmin();

            var vm = new TenantViewModel
            {
                TenantID         = tenant.TenantID,
                UserID           = tenant.UserID,
                Username         = linkedUser?.Username,
                UnitID           = tenant.UnitID,
                FirstName        = tenant.FirstName,
                LastName         = tenant.LastName,
                ContactNumber    = tenant.ContactNumber,
                EmergencyContact = tenant.EmergencyContact,
                MoveInDate       = tenant.MoveInDate,
                MoveOutDate      = tenant.MoveOutDate,
                LeaseStart       = tenant.LeaseStart,
                LeaseEnd         = tenant.LeaseEnd,
                Status           = tenant.Status,
                AvailableUnits   = await GetAllUnitsAsync()
            };
            return View(vm);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Edit(int id, TenantViewModel vm)
        {
            if (id != vm.TenantID) return NotFound();

            bool isSuperAdmin = CurrentUserIsSuperAdmin();

            // Only super admin may change another user's password
            if (!isSuperAdmin || string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            if (string.IsNullOrWhiteSpace(vm.Username))
                ModelState.AddModelError("Username", "Username is required.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsSuperAdmin = isSuperAdmin;
                vm.AvailableUnits = await GetAllUnitsAsync();
                return View(vm);
            }

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // Update linked account if one exists
            if (tenant.UserID.HasValue)
            {
                var linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);
                if (linkedUser != null)
                {
                    // Check username uniqueness (exclude self)
                    bool duplicate = await _context.tblUsers.AnyAsync(u => u.Username == vm.Username && u.UserID != linkedUser.UserID);
                    if (duplicate)
                    {
                        ModelState.AddModelError("Username", "Username already exists.");
                        ViewBag.IsSuperAdmin = isSuperAdmin;
                        vm.AvailableUnits = await GetAllUnitsAsync();
                        return View(vm);
                    }

                    linkedUser.Username = vm.Username!;
                    // Only super admin can reset another user's password
                    if (isSuperAdmin && !string.IsNullOrWhiteSpace(vm.Password))
                        linkedUser.Password = PasswordHelper.Hash(vm.Password);
                }
            }

            int previousUnitID = tenant.UnitID;

            tenant.UnitID           = vm.UnitID;
            tenant.FirstName        = vm.FirstName;
            tenant.LastName         = vm.LastName;
            tenant.ContactNumber    = vm.ContactNumber;
            tenant.EmergencyContact = vm.EmergencyContact;
            tenant.MoveInDate       = vm.MoveInDate;
            tenant.MoveOutDate      = vm.MoveOutDate;
            tenant.LeaseStart       = vm.LeaseStart;
            tenant.LeaseEnd         = vm.LeaseEnd;
            tenant.Status           = vm.Status;

            if (previousUnitID != vm.UnitID)
            {
                var newUnit = await _context.tblUnits.FindAsync(vm.UnitID);
                if (newUnit != null) newUnit.Status = "Occupied";

                bool stillOccupied = await _context.tblTenants
                    .AnyAsync(t => t.UnitID == previousUnitID && t.Status == "Active" && t.TenantID != id);
                if (!stillOccupied)
                {
                    var oldUnit = await _context.tblUnits.FindAsync(previousUnitID);
                    if (oldUnit != null) oldUnit.Status = "Vacant";
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Tenant {tenant.FullName} has been updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.tblTenants.Any(t => t.TenantID == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5  (soft delete confirmation)
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenantID == id);

            if (tenant == null) return NotFound();
            return View(tenant);
        }

        // POST: Tenants/Delete/5  (soft delete — sets status to Inactive)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            tenant.Status = "Inactive";
            tenant.MoveOutDate ??= DateTime.Now;

            bool stillOccupied = await _context.tblTenants
                .AnyAsync(t => t.UnitID == tenant.UnitID && t.Status == "Active" && t.TenantID != id);
            if (!stillOccupied)
            {
                var unit = await _context.tblUnits.FindAsync(tenant.UnitID);
                if (unit != null) unit.Status = "Vacant";
            }

            // Also deactivate the linked login account
            if (tenant.UserID.HasValue)
            {
                var linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);
                if (linkedUser != null) linkedUser.IsActive = false;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Tenant {tenant.FullName} has been set to Inactive.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetAvailableUnitsAsync()
        {
            return await _context.tblUnits
                .Where(u => u.Status == "Vacant")
                .OrderBy(u => u.UnitNumber)
                .Select(u => new SelectListItem
                {
                    Value = u.UnitID.ToString(),
                    Text = $"{u.UnitNumber} — {u.UnitType} (₱{u.RentPrice:N2})"
                })
                .ToListAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetAllUnitsAsync()
        {
            return await _context.tblUnits
                .OrderBy(u => u.UnitNumber)
                .Select(u => new SelectListItem
                {
                    Value = u.UnitID.ToString(),
                    Text = $"{u.UnitNumber} — {u.UnitType} (₱{u.RentPrice:N2}) [{u.Status}]"
                })
                .ToListAsync();
        }
    }
}
