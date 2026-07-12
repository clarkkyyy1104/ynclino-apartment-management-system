using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
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

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // Re-derive a unit's occupancy from its active tenants. A unit under
        // maintenance keeps that status until an admin clears it by hand.
        private async Task SyncUnitStatusAsync(int unitId)
        {
            var unit = await _context.tblUnits.FindAsync(unitId);
            if (unit == null || unit.Status == "Under Maintenance") return;

            bool hasActive = await _context.tblTenants.AnyAsync(t => t.UnitID == unitId && t.Status == "Active");
            unit.Status = hasActive ? "Occupied" : "Vacant";
        }

        // GET: Tenants
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            var query = _context.tblTenants.Include(t => t.Unit).AsQueryable();

            // default view is Active; "All" is an explicit choice that skips filtering
            statusFilter ??= "Active";
            if (statusFilter is "Active" or "Inactive")
                query = query.Where(t => t.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(t => t.FirstName.Contains(searchTerm) || t.LastName.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
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

            // a tenant may only open their own profile
            if (User.IsInRole("Tenant") && tenant.UserID != CurrentUserID())
                return Forbid();

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
            // build a username from first name + move-in MMdd, bump a suffix if taken
            if (!string.IsNullOrWhiteSpace(vm.FirstName) && vm.MoveInDate.HasValue)
            {
                string baseUsername = vm.FirstName.Trim()
                    + vm.MoveInDate.Value.ToString("MMdd");
                string generated = baseUsername;
                int suffix = 2;
                while (await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == generated.ToLower()))
                    generated = baseUsername + "_" + suffix++;
                vm.Username = generated;
            }

            // fall back to a generated password from contact number + initials
            if (string.IsNullOrWhiteSpace(vm.Password)
                && !string.IsNullOrWhiteSpace(vm.ContactNumber)
                && !string.IsNullOrWhiteSpace(vm.FirstName)
                && !string.IsNullOrWhiteSpace(vm.LastName))
            {
                vm.Password = vm.ContactNumber.Trim()
                    + "@"
                    + char.ToUpper(vm.FirstName.Trim()[0])
                    + char.ToLower(vm.LastName.Trim()[0]);
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            // account fields are required on create
            if (string.IsNullOrWhiteSpace(vm.Username))
                ModelState.AddModelError("Username", "Username could not be generated. Ensure First Name and Move-In Date are filled.");
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required. Enter a password or fill in Contact Number and Name.");

            // can't set move-in or lease start in the past
            var today = DateTime.Today;
            if (vm.MoveInDate.HasValue && vm.MoveInDate.Value.Date < today)
                ModelState.AddModelError("MoveInDate", "Move-In Date cannot be in the past.");
            if (vm.LeaseStart.HasValue && vm.LeaseStart.Value.Date < today)
                ModelState.AddModelError("LeaseStart", "Lease Start cannot be in the past.");

            // the posted unit must exist and still have room
            var unit = await _context.tblUnits.FindAsync(vm.UnitID);
            if (unit == null)
                ModelState.AddModelError("UnitID", "Select a valid unit.");
            else if (unit.Status == "Under Maintenance")
                ModelState.AddModelError("UnitID", "That unit is under maintenance and cannot take tenants.");
            else if (await ActiveTenantCountAsync(unit.UnitID) >= unit.Capacity)
                ModelState.AddModelError("UnitID", "That unit is already at full capacity.");

            // flag an obvious duplicate registration
            if (await IsDuplicateTenantAsync(vm.FirstName, vm.LastName, vm.ContactNumber, null))
                ModelState.AddModelError(string.Empty, "An active tenant with the same name and contact number already exists.");

            if (!ModelState.IsValid)
            {
                vm.AvailableUnits = await GetAvailableUnitsAsync();
                return View(vm);
            }

            // create the login account and tenant together so a failure leaves neither behind
            var user = new tblUser
            {
                Username = vm.Username!,
                Password = PasswordHelper.Hash(vm.Password!),
                Role = "Tenant",
                IsActive = true,
                IsSuperAdmin = false,
                DateCreated = DateTime.Now
            };

            var tenant = new tblTenant
            {
                User = user,
                UnitID = vm.UnitID,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                ContactNumber = vm.ContactNumber,
                EmergencyContact = vm.EmergencyContact,
                MoveInDate = vm.MoveInDate,
                MoveOutDate = vm.MoveOutDate,
                LeaseStart = vm.LeaseStart,
                LeaseEnd = vm.LeaseEnd,
                Status = "Active",
                DateRecorded = DateTime.Now
            };
            _context.tblTenants.Add(tenant);

            if (unit!.Status == "Vacant") unit.Status = "Occupied";

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

            // pull the linked account username if there is one
            tblUser? linkedUser = null;
            if (tenant.UserID.HasValue)
                linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);

            ViewBag.IsSuperAdmin = CurrentUserIsSuperAdmin();

            var vm = new TenantViewModel
            {
                TenantID = tenant.TenantID,
                UserID = tenant.UserID,
                Username = linkedUser?.Username,
                UnitID = tenant.UnitID,
                FirstName = tenant.FirstName,
                LastName = tenant.LastName,
                ContactNumber = tenant.ContactNumber,
                EmergencyContact = tenant.EmergencyContact,
                MoveInDate = tenant.MoveInDate,
                MoveOutDate = tenant.MoveOutDate,
                LeaseStart = tenant.LeaseStart,
                LeaseEnd = tenant.LeaseEnd,
                Status = tenant.Status,
                AvailableUnits = await GetAllUnitsAsync()
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

            // only super admin can change another user's password
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

            int previousUnitID = tenant.UnitID;
            string previousStatus = tenant.Status;
            bool becomingActive = vm.Status == "Active";

            // when the tenant is (or is becoming) active, the target unit must be valid and have room
            if (becomingActive)
            {
                var targetUnit = await _context.tblUnits.FindAsync(vm.UnitID);
                if (targetUnit == null)
                    ModelState.AddModelError("UnitID", "Select a valid unit.");
                else
                {
                    int activeInTarget = await ActiveTenantCountAsync(vm.UnitID, excludeTenantId: id);
                    if (targetUnit.Status == "Under Maintenance")
                        ModelState.AddModelError("UnitID", "That unit is under maintenance and cannot take tenants.");
                    else if (activeInTarget >= targetUnit.Capacity)
                        ModelState.AddModelError("UnitID", "That unit is already at full capacity.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.IsSuperAdmin = isSuperAdmin;
                vm.AvailableUnits = await GetAllUnitsAsync();
                return View(vm);
            }

            // the username must be free (ignoring this tenant's own account)
            bool duplicateUsername = await _context.tblUsers
                .AnyAsync(u => u.Username.ToLower() == vm.Username!.ToLower() && u.UserID != tenant.UserID);
            if (duplicateUsername)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsSuperAdmin = isSuperAdmin;
                vm.AvailableUnits = await GetAllUnitsAsync();
                return View(vm);
            }

            if (tenant.UserID.HasValue)
            {
                // update the existing linked account
                var linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);
                if (linkedUser != null)
                {
                    linkedUser.Username = vm.Username!;
                    if (isSuperAdmin && !string.IsNullOrWhiteSpace(vm.Password))
                        linkedUser.Password = PasswordHelper.Hash(vm.Password);

                    // login follows the tenant's active state
                    linkedUser.IsActive = becomingActive;
                }
            }
            else
            {
                // tenant has no account yet — create one, generating a password if none was given
                string password = !string.IsNullOrWhiteSpace(vm.Password)
                    ? vm.Password!
                    : $"{vm.ContactNumber}@{char.ToUpper(vm.FirstName[0])}{char.ToLower(vm.LastName[0])}";

                var newUser = new tblUser
                {
                    Username = vm.Username!,
                    Password = PasswordHelper.Hash(password),
                    Role = "Tenant",
                    IsActive = becomingActive,
                    IsSuperAdmin = false,
                    DateCreated = DateTime.Now
                };
                tenant.User = newUser;
            }

            tenant.UnitID = vm.UnitID;
            tenant.FirstName = vm.FirstName;
            tenant.LastName = vm.LastName;
            tenant.ContactNumber = vm.ContactNumber;
            tenant.EmergencyContact = vm.EmergencyContact;
            tenant.MoveInDate = vm.MoveInDate;
            tenant.LeaseStart = vm.LeaseStart;
            tenant.LeaseEnd = vm.LeaseEnd;
            tenant.Status = vm.Status;

            // stamp a move-out when deactivating, clear it when bringing the tenant back
            if (previousStatus == "Active" && !becomingActive)
                tenant.MoveOutDate = vm.MoveOutDate ?? DateTime.Now;
            else if (becomingActive)
                tenant.MoveOutDate = null;
            else
                tenant.MoveOutDate = vm.MoveOutDate;

            await _context.SaveChangesAsync();

            // re-derive occupancy for both the old and the new unit
            await SyncUnitStatusAsync(previousUnitID);
            if (previousUnitID != vm.UnitID)
                await SyncUnitStatusAsync(vm.UnitID);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5 (soft delete confirmation)
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

        // POST: Tenants/Delete/5 (soft delete - sets status to Inactive)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            tenant.Status = "Inactive";
            tenant.MoveOutDate ??= DateTime.Now;

            // deactivate the linked login account as well
            if (tenant.UserID.HasValue)
            {
                var linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);
                if (linkedUser != null) linkedUser.IsActive = false;
            }

            await _context.SaveChangesAsync();

            await SyncUnitStatusAsync(tenant.UnitID);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been set to Inactive.";
            return RedirectToAction(nameof(Index));
        }

        private Task<int> ActiveTenantCountAsync(int unitId, int? excludeTenantId = null) =>
            _context.tblTenants.CountAsync(t =>
                t.UnitID == unitId &&
                t.Status == "Active" &&
                (excludeTenantId == null || t.TenantID != excludeTenantId));

        private Task<bool> IsDuplicateTenantAsync(string firstName, string lastName, string? contact, int? excludeTenantId) =>
            _context.tblTenants.AnyAsync(t =>
                t.Status == "Active" &&
                t.FirstName == firstName &&
                t.LastName == lastName &&
                t.ContactNumber == contact &&
                (excludeTenantId == null || t.TenantID != excludeTenantId));

        // units that aren't under maintenance and still have a free slot
        private async Task<IEnumerable<SelectListItem>> GetAvailableUnitsAsync()
        {
            return await _context.tblUnits
                .Where(u => u.Status != "Under Maintenance"
                            && u.Tenants.Count(t => t.Status == "Active") < u.Capacity)
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
