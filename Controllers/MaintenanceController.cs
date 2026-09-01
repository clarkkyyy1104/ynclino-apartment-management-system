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

        // maintenance staff see ONLY the requests assigned to them
        private bool IsStaff() => User.IsInRole("Maintenance");

        // every active maintenance staff account, for the "Assign To" dropdown
        private async Task<IEnumerable<SelectListItem>> GetStaffListAsync()
        {
            return await _context.tblUsers
                .Where(u => u.Role == "Maintenance" && u.IsActive)
                .OrderBy(u => u.Username)
                .Select(u => new SelectListItem { Value = u.UserID.ToString(), Text = u.Username })
                .ToListAsync();
        }
        private static readonly string[] Categories = { "Plumbing", "Electrical", "Structural", "Appliance", "Other" };
        private static readonly string[] Priorities = { "Minor", "Moderate", "Major", "Urgent" };
        private static readonly string[] Statuses = { "Pending", "In Progress", "Resolved", "Cancelled" };
        // resolved/cancelled requests move out of the active list into the archive
        private static readonly string[] ArchivedStatuses = { "Resolved", "Cancelled" };

        // GET: Maintenance
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm, bool archived = false)
        {
            IQueryable<tblMaintenanceRequest> query = _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .Include(m => m.Unit)
                .Include(m => m.AssignedStaff);

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return View(new List<tblMaintenanceRequest>());
                query = query.Where(m => m.TenantID == tenant.TenantID);
            }
            else if (IsStaff())
            {
                // staff only ever see their own assigned work — enforced here in the
                // query, not just hidden in the view
                var me = CurrentUserID() ?? 0;
                query = query.Where(m => m.AssignedStaffID == me);
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
            var uid = CurrentUserID();
            ViewBag.UnreadIds = uid == null ? new HashSet<int>() : await NotificationHelper.UnreadTargetIdsAsync(_context, uid.Value, "Maintenance");
            ViewBag.ReadIds = uid == null ? new HashSet<int>() : await NotificationHelper.ReadTargetIdsAsync(_context, uid.Value, "Maintenance");

            return View(await query.OrderByDescending(m => m.DateSubmitted).ToListAsync());
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .Include(m => m.Unit)
                .Include(m => m.AssignedStaff)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null || request.TenantID != tenant.TenantID) return Forbid();
            }
            else if (IsStaff() && request.AssignedStaffID != CurrentUserID())
            {
                // a staff member cannot open somebody else's job by typing its URL
                return Forbid();
            }
            return View(request);
        }

        // GET: Maintenance/Create — staff carry out work, they do not raise requests
        [Authorize(Roles = "Admin,Tenant")]
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
        [Authorize(Roles = "Admin,Tenant")]
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

            // stamp the unit at the moment the request is made, so the repair stays
            // attached to the apartment even if this tenant later moves out
            var owner = await _context.tblTenants.FirstOrDefaultAsync(t => t.TenantID == vm.TenantID);

            var request = new tblMaintenanceRequest
            {
                TenantID = vm.TenantID,
                UnitID = owner?.UnitID,
                Category = vm.Category,
                Description = vm.Description ?? string.Empty,
                Priority = vm.Priority,
                Status = "Pending",
                DateSubmitted = DateTime.Now,
                ImagePath = imagePath
            };

            _context.tblMaintenanceRequests.Add(request);
            await _context.SaveChangesAsync();

            // a tenant-submitted request alerts the administrators
            if (User.IsInRole("Tenant"))
            {
                await NotificationHelper.NotifyAdminsAsync(_context, "Maintenance",
                    $"New {vm.Category} maintenance request from {owner?.FullName}.",
                    $"/Maintenance/Details/{request.RequestID}", request.RequestID);
            }

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

            // resolved/cancelled requests are archived history — view only
            if (StatusFlowHelper.IsClosedMaintenance(request.Status))
            {
                TempData["Error"] = $"This request is already \"{request.Status}\" and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id = request.RequestID });
            }

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
                StaffNotes = request.StaffNotes,
                AssignedStaffID = request.AssignedStaffID ?? 0,
                Cost = request.Cost,
                ImagePath = request.ImagePath
            };
            vm.AvailableStaff = await GetStaffListAsync();
            // a resolved/cancelled request cannot be reopened
            ViewBag.AllowedStatuses = StatusFlowHelper.AllowedMaintenance(request.Status);
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

            // closed requests are view-only — reject the post outright
            if (StatusFlowHelper.IsClosedMaintenance(request.Status))
            {
                TempData["Error"] = $"This request is already \"{request.Status}\" and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // status may only move forward — a resolved request cannot be reopened
            if (!StatusFlowHelper.IsAllowedMaintenance(request.Status, vm.Status))
                ModelState.AddModelError("Status",
                    $"This request is already \"{request.Status}\" — it cannot be moved back to \"{vm.Status}\".");

            // 0 means "leave it unassigned"; any other value must be a real staff account
            if (vm.AssignedStaffID != 0 &&
                !await _context.tblUsers.AnyAsync(u => u.UserID == vm.AssignedStaffID && u.Role == "Maintenance" && u.IsActive))
                ModelState.AddModelError("AssignedStaffID", "Select a valid maintenance staff account.");

            if (!ModelState.IsValid)
            {
                vm.TenantName = request.Tenant?.FullName;
                vm.UnitNumber = request.Tenant?.Unit?.UnitNumber;
                vm.ImagePath = request.ImagePath;
                vm.AvailableStaff = await GetStaffListAsync();
                ViewBag.AllowedStatuses = StatusFlowHelper.AllowedMaintenance(request.Status);
                return View(vm);
            }

            var previousStatus = request.Status;
            var previousStaff = request.AssignedStaffID;

            request.Category = vm.Category;
            request.Description = vm.Description ?? string.Empty;
            request.Priority = vm.Priority;
            request.Status = vm.Status;
            request.AdminNotes = vm.AdminNotes;
            request.Cost = vm.Cost;
            request.AssignedStaffID = vm.AssignedStaffID == 0 ? null : vm.AssignedStaffID;

            if (vm.ImageUpload != null)
                request.ImagePath = await ImageUploadHelper.SaveAsync(vm.ImageUpload, "maintenance", _env);

            if (vm.Status == "Resolved" && request.DateResolved == null)
                request.DateResolved = DateTime.Now;
            else if (vm.Status != "Resolved")
                request.DateResolved = null;

            await _context.SaveChangesAsync();

            // tell the tenant when staff change the status of their request
            if (request.Tenant?.UserID != null && previousStatus != request.Status)
                await NotificationHelper.CreateAsync(_context, request.Tenant.UserID.Value, "Maintenance",
                    $"Your {request.Category} request was updated to \"{request.Status}\".",
                    $"/Maintenance/Details/{request.RequestID}", request.RequestID);

            // tell the staff member the moment a job lands on their plate
            if (request.AssignedStaffID != null && request.AssignedStaffID != previousStaff)
                await NotificationHelper.CreateAsync(_context, request.AssignedStaffID.Value, "Maintenance",
                    $"You have been assigned a {request.Priority} {request.Category} request.",
                    $"/Maintenance/Details/{request.RequestID}", request.RequestID);

            TempData["Success"] = "Maintenance request updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Work/5 — the maintenance staff's own, narrow form.
        // They may only move the status forward, write work notes, and record the cost.
        [Authorize(Roles = "Maintenance")]
        public async Task<IActionResult> Work(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();
            if (request.AssignedStaffID != CurrentUserID()) return Forbid();

            if (StatusFlowHelper.IsClosedMaintenance(request.Status))
            {
                TempData["Error"] = $"This request is already \"{request.Status}\" and can no longer be updated.";
                return RedirectToAction(nameof(Details), new { id = request.RequestID });
            }

            ViewBag.AllowedStatuses = StatusFlowHelper.AllowedMaintenance(request.Status);
            return View(new MaintenanceViewModel
            {
                RequestID = request.RequestID,
                TenantID = request.TenantID,
                TenantName = request.Tenant?.FullName,
                UnitNumber = request.Unit?.UnitNumber ?? request.Tenant?.Unit?.UnitNumber,
                Category = request.Category,
                Description = request.Description,
                Priority = request.Priority,
                Status = request.Status,
                DateSubmitted = request.DateSubmitted,
                StaffNotes = request.StaffNotes,
                Cost = request.Cost,
                ImagePath = request.ImagePath
            });
        }

        // POST: Maintenance/Work/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Maintenance")]
        public async Task<IActionResult> Work(int id, MaintenanceViewModel vm)
        {
            if (id != vm.RequestID) return NotFound();

            var request = await _context.tblMaintenanceRequests
                .Include(m => m.Tenant).ThenInclude(t => t!.Unit)
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.RequestID == id);

            if (request == null) return NotFound();

            // re-check ownership on the POST: hiding the button is not security
            if (request.AssignedStaffID != CurrentUserID()) return Forbid();

            if (StatusFlowHelper.IsClosedMaintenance(request.Status))
            {
                TempData["Error"] = $"This request is already \"{request.Status}\" and can no longer be updated.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!StatusFlowHelper.IsAllowedMaintenance(request.Status, vm.Status))
                ModelState.AddModelError("Status",
                    $"This request is already \"{request.Status}\" — it cannot be moved back to \"{vm.Status}\".");

            // the staff form posts none of these, so their validation must not block it
            ModelState.Remove(nameof(vm.Category));
            ModelState.Remove(nameof(vm.Priority));
            ModelState.Remove(nameof(vm.Description));

            if (!ModelState.IsValid)
            {
                vm.TenantName = request.Tenant?.FullName;
                vm.UnitNumber = request.Unit?.UnitNumber ?? request.Tenant?.Unit?.UnitNumber;
                vm.Category = request.Category;
                vm.Priority = request.Priority;
                vm.Description = request.Description;
                vm.ImagePath = request.ImagePath;
                ViewBag.AllowedStatuses = StatusFlowHelper.AllowedMaintenance(request.Status);
                return View(vm);
            }

            var previousStatus = request.Status;

            // ONLY these three fields — a staff member cannot change the category,
            // the priority, the tenant, or who the job is assigned to
            request.Status = vm.Status;
            request.StaffNotes = vm.StaffNotes;
            request.Cost = vm.Cost;

            if (vm.Status == "Resolved" && request.DateResolved == null)
                request.DateResolved = DateTime.Now;

            await _context.SaveChangesAsync();

            if (previousStatus != request.Status)
            {
                if (request.Tenant?.UserID != null)
                    await NotificationHelper.CreateAsync(_context, request.Tenant.UserID.Value, "Maintenance",
                        $"Your {request.Category} request was updated to \"{request.Status}\".",
                        $"/Maintenance/Details/{request.RequestID}", request.RequestID);

                await NotificationHelper.NotifyAdminsAsync(_context, "Maintenance",
                    $"{User.Identity?.Name} marked a {request.Category} request as \"{request.Status}\".",
                    $"/Maintenance/Details/{request.RequestID}", request.RequestID);
            }

            TempData["Success"] = "Request updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Maintenance/Cancel/5 — a tenant withdraws their own pending request
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Tenant")]
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
