using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaintenanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        private async Task<tblTenant?> GetCurrentTenantAsync()
        {
            var uid = CurrentUserID();
            if (uid == null) return null;
            return await _context.tblTenants.FirstOrDefaultAsync(t => t.UserID == uid && t.Status == "Active");
        }

        // GET: Maintenance
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            IQueryable<tblMaintenanceRequest> query = _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit);

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return View(new List<tblMaintenanceRequest>());
                query = query.Where(m => m.TenantID == tenant.TenantID);
            }

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(m => m.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(m =>
                    m.Tenant!.FirstName.Contains(searchTerm) ||
                    m.Tenant!.LastName.Contains(searchTerm) ||
                    m.Category.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

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
                if (tenant != null)
                {
                    vm.TenantID = tenant.TenantID;
                    vm.TenantName = tenant.FullName;
                    vm.UnitNumber = tenant.Unit?.UnitNumber;
                }
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
            if (!ModelState.IsValid)
            {
                if (!User.IsInRole("Tenant"))
                    vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            // tenants can only submit for themselves
            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();
                vm.TenantID = tenant.TenantID;
            }

            var request = new tblMaintenanceRequest
            {
                TenantID = vm.TenantID,
                Category = vm.Category,
                Description = vm.Description,
                Priority = vm.Priority,
                Status = "Pending",
                DateSubmitted = DateTime.Now
            };

            _context.tblMaintenanceRequests.Add(request);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request submitted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Edit/5
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
                AvailableTenants = await GetActiveTenantListAsync()
            };
            return View(vm);
        }

        // POST: Maintenance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaintenanceViewModel vm)
        {
            if (id != vm.RequestID) return NotFound();
            if (!ModelState.IsValid)
            {
                vm.AvailableTenants = await GetActiveTenantListAsync();
                return View(vm);
            }

            var request = await _context.tblMaintenanceRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Category = vm.Category;
            request.Description = vm.Description;
            request.Priority = vm.Priority;
            request.Status = vm.Status;
            request.AdminNotes = vm.AdminNotes;

            if (vm.Status == "Resolved" && request.DateResolved == null)
                request.DateResolved = DateTime.Now;
            else if (vm.Status != "Resolved")
                request.DateResolved = null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Delete/5
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.tblMaintenanceRequests.FindAsync(id);
            if (request == null) return NotFound();

            _context.tblMaintenanceRequests.Remove(request);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maintenance request deleted.";
            return RedirectToAction(nameof(Index));
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
