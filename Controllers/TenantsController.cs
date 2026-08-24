using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TenantsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TenantsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Tenants
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            var query = _context.tblTenants.AsQueryable();

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

            var tenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == id);
            if (tenant == null) return NotFound();

            return View(tenant);
        }

        // GET: Tenants/Create
        public IActionResult Create()
        {
            return View(new TenantViewModel());
        }

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TenantViewModel vm)
        {
            if (await IsDuplicateTenantAsync(vm.FirstName, vm.LastName, vm.ContactNumber, null))
                ModelState.AddModelError(string.Empty, "An active tenant with the same name and contact number already exists.");

            if (!ModelState.IsValid)
                return View(vm);

            var tenant = new tblTenant
            {
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                ContactNumber = vm.ContactNumber,
                EmergencyContactName = vm.EmergencyContactName,
                EmergencyContactRelationship = vm.EmergencyContactRelationship,
                EmergencyContactNumber = vm.EmergencyContactNumber,
                MoveInDate = vm.MoveInDate,
                Status = "Active",
                DateRecorded = DateTime.Now
            };
            _context.tblTenants.Add(tenant);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been registered.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            var vm = new TenantViewModel
            {
                TenantID = tenant.TenantID,
                FirstName = tenant.FirstName,
                LastName = tenant.LastName,
                ContactNumber = tenant.ContactNumber,
                EmergencyContactName = tenant.EmergencyContactName,
                EmergencyContactRelationship = tenant.EmergencyContactRelationship,
                EmergencyContactNumber = tenant.EmergencyContactNumber,
                MoveInDate = tenant.MoveInDate,
                MoveOutDate = tenant.MoveOutDate,
                Status = tenant.Status
            };
            return View(vm);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TenantViewModel vm)
        {
            if (id != vm.TenantID) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            string previousStatus = tenant.Status;
            bool becomingActive = vm.Status == "Active";

            tenant.FirstName = vm.FirstName;
            tenant.LastName = vm.LastName;
            tenant.ContactNumber = vm.ContactNumber;
            tenant.EmergencyContactName = vm.EmergencyContactName;
            tenant.EmergencyContactRelationship = vm.EmergencyContactRelationship;
            tenant.EmergencyContactNumber = vm.EmergencyContactNumber;
            tenant.MoveInDate = vm.MoveInDate;
            tenant.Status = vm.Status;

            // stamp a move-out when deactivating, clear it when bringing the tenant back
            if (previousStatus == "Active" && !becomingActive)
                tenant.MoveOutDate = DateTime.Now;
            else if (becomingActive)
                tenant.MoveOutDate = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5 (soft delete confirmation)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == id);
            if (tenant == null) return NotFound();
            return View(tenant);
        }

        // POST: Tenants/Delete/5 (soft delete - sets status to Inactive)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tenant = await _context.tblTenants.FindAsync(id);
            if (tenant == null) return NotFound();

            tenant.Status = "Inactive";
            tenant.MoveOutDate ??= DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tenant {tenant.FullName} has been set to Inactive.";
            return RedirectToAction(nameof(Index));
        }

        private Task<bool> IsDuplicateTenantAsync(string firstName, string lastName, string? contact, int? excludeTenantId) =>
            _context.tblTenants.AnyAsync(t =>
                t.Status == "Active" &&
                t.FirstName == firstName &&
                t.LastName == lastName &&
                t.ContactNumber == contact &&
                (excludeTenantId == null || t.TenantID != excludeTenantId));
    }
}
