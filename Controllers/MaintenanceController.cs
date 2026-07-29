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
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public MaintenanceController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private async Task<tblTenant?> GetCurrentTenantAsync()
        {
            var uid = CurrentUserID();
            if (uid == null) return null;
            return await _context.tblTenants
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.UserID == uid && t.Status == "Active");
        }

        private static readonly string[] Categories = { "Plumbing", "Electrical", "Structural", "Appliance", "Other" };
        private static readonly string[] Priorities = { "Low", "Medium", "High", "Urgent" };
        private static readonly string[] Statuses = { "Pending", "In Progress", "Resolved", "Cancelled" };
        // resolved/cancelled requests move out of the active list into the archive
        private static readonly string[] ArchivedStatuses = { "Resolved", "Cancelled" };

        // GET: Maintenance
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm, bool archived = false)
        {
            IQueryable<tblMaintenanceRequest> query = _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit);

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return View(new List<tblMaintenanceRequest>());
                query = query.Where(m => m.TenantID == tenant.TenantID);
            }

            // the archive holds resolved/cancelled requests; the main list holds active ones
            if (archived)
                query = query.Where(m => ArchivedStatuses.Contains(m.Status));
            else
                query = query.Where(m => !ArchivedStatuses.Contains(m.Status));

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(m => m.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(m =>
                    m.Tenant!.FirstName.Contains(searchTerm) ||
                    m.Tenant!.LastName.Contains(searchTerm) ||
                    m.Category.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Archived = archived;

            return View(await query.OrderByDescending(m => m.DateSubmitted).ToListAsync());
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null || request.TenantID != tenant.TenantID) return Forbid();
            }

            return View(request);
        }

        // GET: Maintenance/Create
        public async Task<IActionResult> Create()
        {
            var vm = new MaintenanceViewModel();

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null)
                {
                    TempData["Error"] = "You have no active tenancy on file, so you cannot submit a request.";
                    return RedirectToAction(nameof(Index));
                }
                vm.TenantID = tenant.TenantID;
                vm.TenantName = tenant.FullName;
                vm.UnitNumber = tenant.Unit?.UnitNumber;
            }
            else
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
            }

            return View(vm);
        }

        // POST: Maintenance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceViewModel vm)
        {
            if (!Categories.Contains(vm.Category))
                ModelState.AddModelError("Category", "Select a valid issue type.");
            if (!Priorities.Contains(vm.Priority))
                ModelState.AddModelError("Priority", "Select a valid priority.");

            // a description is only required when the issue type is "Other";
            // for the preset types we fall back to the type itself
            NormalizeDescription(vm);

            if (vm.ImageUpload != null && !ImageUploadHelper.IsValid(vm.ImageUpload, out var imgErr))
                ModelState.AddModelError(nameof(vm.ImageUpload), imgErr);

            // tenants can only submit for themselves
            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();
                vm.TenantID = tenant.TenantID;
            }
            else if (!await _context.tblTenants.AnyAsync(t => t.TenantID == vm.TenantID && t.Status == "Active"))
            {
                ModelState.AddModelError("TenantID", "Select a valid tenant.");
            }

            if (!ModelState.IsValid)
            {
                if (!User.IsInRole("Tenant"))
                    vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            string? imagePath = null;
            if (vm.ImageUpload != null)
                imagePath = await ImageUploadHelper.SaveAsync(vm.ImageUpload, "maintenance", _env);

            var request = new tblMaintenanceRequest
            {
                TenantID = vm.TenantID,
                Category = vm.Category,
                Description = vm.Description ?? string.Empty,
                Priority = vm.Priority,
                Status = "Pending",
                DateSubmitted = DateTime.Now,
                ImagePath = imagePath
            };

            _context.tblMaintenanceRequests.Add(request);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request submitted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();

            var vm = new MaintenanceViewModel
            {
                RequestID = request.RequestID,
                TenantID = request.TenantID,
                TenantName = request.Tenant?.FullName,
                UnitNumber = request.Tenant?.Unit?.UnitNumber,
                Category = request.Category,
                Description = request.Description,
                Priority = request.Priority,
                Status = request.Status,
                DateSubmitted = request.DateSubmitted,
                DateResolved = request.DateResolved,
                AdminNotes = request.AdminNotes,
                ImagePath = request.ImagePath
            };
            return View(vm);
        }

        // POST: Maintenance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, MaintenanceViewModel vm)
        {
            if (id != vm.RequestID) return NotFound();

            if (!Categories.Contains(vm.Category))
                ModelState.AddModelError("Category", "Select a valid issue type.");
            if (!Priorities.Contains(vm.Priority))
                ModelState.AddModelError("Priority", "Select a valid priority.");
            if (!Statuses.Contains(vm.Status))
                ModelState.AddModelError("Status", "Select a valid status.");

            NormalizeDescription(vm);

            if (vm.ImageUpload != null && !ImageUploadHelper.IsValid(vm.ImageUpload, out var imgErr))
                ModelState.AddModelError(nameof(vm.ImageUpload), imgErr);

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);
            if (request == null) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.TenantName = request.Tenant?.FullName;
                vm.UnitNumber = request.Tenant?.Unit?.UnitNumber;
                vm.ImagePath = request.ImagePath;
                return View(vm);
            }

            request.Category = vm.Category;
            request.Description = vm.Description ?? string.Empty;
            request.Priority = vm.Priority;
            request.Status = vm.Status;
            request.AdminNotes = vm.AdminNotes;

            if (vm.ImageUpload != null)
                request.ImagePath = await ImageUploadHelper.SaveAsync(vm.ImageUpload, "maintenance", _env);

            if (vm.Status == "Resolved" && request.DateResolved == null)
                request.DateResolved = DateTime.Now;
            else if (vm.Status != "Resolved")
                request.DateResolved = null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Maintenance/Cancel/5 — a tenant withdraws their own pending request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var request = await _context.tblMaintenanceRequests.FindAsync(id);
            if (request == null) return NotFound();

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null || request.TenantID != tenant.TenantID) return Forbid();
            }

            if (request.Status != "Pending")
            {
                TempData["Error"] = "Only a pending request can be cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            request.Status = "Cancelled";
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request cancelled.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();
            return View(request);
        }

        // POST: Maintenance/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.tblMaintenanceRequests.FindAsync(id);
            if (request == null) return NotFound();

            _context.tblMaintenanceRequests.Remove(request);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request deleted.";
            return RedirectToAction(nameof(Index));
        }

        // A description is only mandatory when the issue type is "Other". For the
        // preset types, an empty description falls back to the type name so the
        // record is never blank.
        private void NormalizeDescription(MaintenanceViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Description))
            {
                if (vm.Category == "Other")
                    ModelState.AddModelError(nameof(vm.Description), "Please describe the issue.");
                else
                    vm.Description = vm.Category;
            }
        }

        private async Task<IEnumerable<SelectListItem>> GetActiveTenantListAsync()
        {
            return await _context.tblTenants
                .Include(t => t.Unit)
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.LastName)
                .Select(t => new SelectListItem
                {
                    Value = t.TenantID.ToString(),
                    Text = $"{t.LastName}, {t.FirstName} — Unit {t.Unit!.UnitNumber}"
                })
                .ToListAsync();
        }
    }
}
