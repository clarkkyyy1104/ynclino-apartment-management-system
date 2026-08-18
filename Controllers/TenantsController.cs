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

        private bool CurrentUserIsMainAdmin()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out int id)) return false;
            return _context.tblUsers.Any(u => u.UserID == id && u.IsMainAdmin);
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // Re-derive a unit's occupancy from its active tenants: the unit only
        // reads "Occupied" once it is at full capacity, so partially filled
        // bedspacers stay available. A unit under maintenance keeps that
        // status until an admin clears it by hand.
        private Task SyncUnitStatusAsync(int unitId) => UnitStatusHelper.RefreshAsync(_context, unitId);

        // school-style login username: [2-digit year]-[2-digit month] + the uppercase
        // initials of the first and last name, e.g. Ana Cruz in July 2026 -> "26-07AC"
        private static string GenerateUsername(string firstName, string lastName)
        {
            var now = DateTime.Now;
            string initials = $"{char.ToUpper(firstName.Trim()[0])}{char.ToUpper(lastName.Trim()[0])}";
            return $"{now:yy}-{now:MM}{initials}";
        }

        // GET: Tenants
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            var query = _context.tblTenants.Include(t => t.Unit).Include(t => t.User).AsQueryable();

            // default view is Active; "All" is an explicit choice that skips filtering
            statusFilter ??= "Active";
            if (statusFilter is "Active" or "Inactive")
                query = query.Where(t => t.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(t => t.FirstName.Contains(searchTerm) || t.LastName.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

            // list in username order (the school-style ID) rather than by name
            var tenants = await query.OrderBy(t => t.User!.Username).ToListAsync();
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(TenantViewModel vm)
        {
            // the username is a school-style ID: registration month + the tenant's initials
            if (!string.IsNullOrWhiteSpace(vm.FirstName) && !string.IsNullOrWhiteSpace(vm.LastName))
                vm.Username = GenerateUsername(vm.FirstName, vm.LastName);

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
                ModelState.AddModelError("Username", "Username could not be generated. Ensure First Name and Last Name are filled.");
            else if (await _context.tblUsers.AnyAsync(u => u.Username.ToLower() == vm.Username.ToLower()))
                ModelState.AddModelError(string.Empty, $"Username '{vm.Username}' is already taken (same month and initials). Adjust the name.");
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError("Password", "Password is required. Enter a password or fill in Contact Number and Name.");

            // a unit is not assigned at registration — the tenant applies for one later
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
                IsMainAdmin = false,
                // the admin sets a temporary password; the tenant must change it on first login
                MustChangePassword = true,
                DateCreated = DateTime.Now
            };

            var tenant = new tblTenant
            {
                User = user,
                // allow the admin to assign a unit during registration; vm.UnitID may be null
                UnitID = vm.UnitID,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                ContactNumber = vm.ContactNumber,
                EmergencyContactName = vm.EmergencyContactName,
                EmergencyContactRelationship = vm.EmergencyContactRelationship,
                EmergencyContactNumber = vm.EmergencyContactNumber,
                Status = "Active",
                DateRecorded = DateTime.Now
            };
            _context.tblTenants.Add(tenant);
            await _context.SaveChangesAsync();
            // if a unit was assigned at creation, refresh its computed status
            if (tenant.UnitID.HasValue)
            {
                await SyncUnitStatusAsync(tenant.UnitID.Value);
                await _context.SaveChangesAsync();

                var unit = await _context.tblUnits.FindAsync(tenant.UnitID.Value);
                if (unit != null) 
                {
                    decimal moveInTotal = unit.Deposit + unit.AdvancePayment;
                    _context.tblBillings.Add(new tblBilling
                    {
                        TenantID = tenant.TenantID,
                        BillingPeriod = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                        AmountDue = moveInTotal,
                        AmountPaid = moveInTotal,
                        DueDate = DateTime.Today,
                        DatePaid = DateTime.Today,
                        Status = "Paid",
                        Notes = $"Move-in payment - Deposit ₱{unit.Deposit:N2} + Advance Payment ₱{unit.AdvancePayment:N2}",
                        DateIssued = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
                TempData["Success"] = $"Tenant {tenant.FullName} has been registered and assigned to unit {unit?.UnitNumber}.";
            }
            else
            {
                TempData["Success"] = $"Tenant {tenant.FullName} has been registered with account '{user.Username}'.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // pull the linked account username if there is one
            tblUser? linkedUser = null;
            if (tenant.UserID.HasValue)
                linkedUser = await _context.tblUsers.FindAsync(tenant.UserID.Value);

            ViewBag.IsMainAdmin = CurrentUserIsMainAdmin();

            var vm = new TenantViewModel
            {
                TenantID = tenant.TenantID,
                UserID = tenant.UserID,
                Username = linkedUser?.Username,
                UnitID = tenant.UnitID,
                FirstName = tenant.FirstName,
                LastName = tenant.LastName,
                ContactNumber = tenant.ContactNumber,
                EmergencyContactName = tenant.EmergencyContactName,
                EmergencyContactRelationship = tenant.EmergencyContactRelationship,
                EmergencyContactNumber = tenant.EmergencyContactNumber,
                MoveOutDate = tenant.MoveOutDate,
                Status = tenant.Status,
                AvailableUnits = await GetAllUnitsAsync()
            };
            return View(vm);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, TenantViewModel vm)
        {
            if (id != vm.TenantID) return NotFound();

            bool isMainAdmin = CurrentUserIsMainAdmin();

            // only the main admin can change another user's password
            if (!isMainAdmin || string.IsNullOrWhiteSpace(vm.Password))
            {
                ModelState.Remove("Password");
                ModelState.Remove("ConfirmPassword");
            }

            if (string.IsNullOrWhiteSpace(vm.Username))
                ModelState.AddModelError("Username", "Username is required.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsMainAdmin = isMainAdmin;
                vm.AvailableUnits = await GetAllUnitsAsync();
                return View(vm);
            }

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // the tenant's unit is managed through the unit-application/approval flow,
            // not edited here, so it is left untouched below
            int? previousUnitID = tenant.UnitID;
            string previousStatus = tenant.Status;
            bool becomingActive = vm.Status == "Active";

            // the username must be free (ignoring this tenant's own account)
            bool duplicateUsername = await _context.tblUsers
                .AnyAsync(u => u.Username.ToLower() == vm.Username!.ToLower() && u.UserID != tenant.UserID);
            if (duplicateUsername)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.IsMainAdmin = isMainAdmin;
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
                    if (isMainAdmin && !string.IsNullOrWhiteSpace(vm.Password))
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
                    IsMainAdmin = false,
                    DateCreated = DateTime.Now
                };
                tenant.User = newUser;
            }

            tenant.FirstName = vm.FirstName;
            tenant.LastName = vm.LastName;
            tenant.ContactNumber = vm.ContactNumber;
            tenant.EmergencyContactName = vm.EmergencyContactName;
            tenant.EmergencyContactRelationship = vm.EmergencyContactRelationship;
            tenant.EmergencyContactNumber = vm.EmergencyContactNumber;
            tenant.Status = vm.Status;

            // move-out date is admin-controlled: an active tenant never has one; an
            // inactive tenant uses the date the admin entered, falling back to "now"
            // the moment they are deactivated (so it's never left blank on move-out)
            if (becomingActive)
                tenant.MoveOutDate = null;
            else
                tenant.MoveOutDate = vm.MoveOutDate
                    ?? (previousStatus == "Active" ? DateTime.Now : tenant.MoveOutDate);

            await _context.SaveChangesAsync();

            // re-derive occupancy for the tenant's unit (their active state may have changed)
            if (previousUnitID.HasValue)
                await SyncUnitStatusAsync(previousUnitID.Value);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5 (soft delete confirmation)
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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

            if (tenant.UnitID.HasValue)
                await SyncUnitStatusAsync(tenant.UnitID.Value);
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
