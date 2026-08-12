using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransfersController(ApplicationDbContext context)
        {
            _context = context;
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

        // reviewed/closed requests move out of the active list into the archive
        private static readonly string[] ArchivedStatuses = { "Approved", "Rejected", "Cancelled" };

        // GET: Transfers  — shows Active and Archived side by side (two columns)
        public async Task<IActionResult> Index()
        {
            IQueryable<tblUnitTransferRequest> query = _context.tblUnitTransferRequests
                .Include(r => r.Tenant).ThenInclude(t => t!.Unit)
                .Include(r => r.CurrentUnit)
                .Include(r => r.RequestedUnit);

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null)
                {
                    ViewBag.ArchivedList = new List<tblUnitTransferRequest>();
                    return View(new List<tblUnitTransferRequest>());
                }
                query = query.Where(r => r.TenantID == tenant.TenantID);
                ViewBag.HasUnit = tenant.UnitID != null;   // drives "Apply for a Unit" vs "Request Transfer"
            }

            var all = await query.ToListAsync();
            var active = all.Where(r => !ArchivedStatuses.Contains(r.Status))
                            .OrderByDescending(r => r.DateRequested).ToList();
            var archived = all.Where(r => ArchivedStatuses.Contains(r.Status))
                              .OrderByDescending(r => r.DateReviewed).ToList();
            ViewBag.ArchivedList = archived;

            var meId = CurrentUserID();
            ViewBag.UnreadIds = meId == null ? new HashSet<int>() : await NotificationHelper.UnreadTargetIdsAsync(_context, meId.Value, "Transfer");
            ViewBag.ReadIds = meId == null ? new HashSet<int>() : await NotificationHelper.ReadTargetIdsAsync(_context, meId.Value, "Transfer");
            return View(active);
        }

        // GET: Transfers/Create  (tenant picks a available unit and gives a reason)
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create()
        {
            var tenant = await GetCurrentTenantAsync();
            if (tenant == null)
            {
                TempData["Error"] = "You have no active tenancy on file, so you cannot request a transfer.";
                return RedirectToAction(nameof(Index));
            }

            if (await HasPendingAsync(tenant.TenantID))
            {
                TempData["Error"] = "You already have a pending transfer request. Please wait for it to be reviewed.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateAvailableUnitsAsync(tenant);
            ViewBag.CurrentUnit = tenant.Unit?.UnitNumber;
            ViewBag.HasUnit = tenant.UnitID != null;
            return View();
        }

        // POST: Transfers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create(int requestedUnitID, string? reason)
        {
            var tenant = await GetCurrentTenantAsync();
            if (tenant == null) return Forbid();

            if (await HasPendingAsync(tenant.TenantID))
            {
                TempData["Error"] = "You already have a pending transfer request.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _context.tblUnits.FindAsync(requestedUnitID);
            if (target == null || requestedUnitID == tenant.UnitID)
                ModelState.AddModelError("requestedUnitID", "Choose a different, available unit.");
            else if (target.Status != "Available" || !await UnitHasRoomAsync(target))
                ModelState.AddModelError("requestedUnitID", "That unit is no longer available (it may be occupied or already reserved).");

            if (!ModelState.IsValid)
            {
                await PopulateAvailableUnitsAsync(tenant);
                ViewBag.CurrentUnit = tenant.Unit?.UnitNumber;
                ViewBag.HasUnit = tenant.UnitID != null;
                return View();
            }

            bool isApplication = tenant.UnitID == null;
            var newRequest = new tblUnitTransferRequest
            {
                TenantID = tenant.TenantID,
                CurrentUnitID = tenant.UnitID,          // null when applying for a first unit
                RequestedUnitID = requestedUnitID,
                Reason = reason?.Trim() ?? string.Empty,
                Status = "Pending",
                DateRequested = DateTime.Now
            };
            _context.tblUnitTransferRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            // the requested unit is now spoken for → mark it Reserved
            await UnitStatusHelper.RefreshAsync(_context, requestedUnitID);
            await _context.SaveChangesAsync();

            await NotificationHelper.NotifyAdminsAsync(_context, "Transfer",
                isApplication ? $"{tenant.FullName} applied for a unit." : $"{tenant.FullName} requested a unit transfer.",
                "/Transfers", newRequest.TransferID);

            TempData["Success"] = isApplication
                ? "Your unit application has been submitted."
                : "Your unit transfer request has been submitted.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/Cancel/5  (tenant withdraws their own pending request)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Cancel(int id)
        {
            var tenant = await GetCurrentTenantAsync();
            if (tenant == null) return Forbid();

            var req = await _context.tblUnitTransferRequests.FindAsync(id);
            if (req == null) return NotFound();

            // a tenant may only cancel their own request while it is still pending
            if (req.TenantID != tenant.TenantID) return Forbid();
            if (req.Status != "Pending")
            {
                TempData["Error"] = "Only a pending request can be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            req.Status = "Cancelled";
            req.DateReviewed = DateTime.Now;
            await _context.SaveChangesAsync();

            // release the reservation on the requested unit
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your transfer request has been cancelled.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/Approve/5  (admin moves the tenant)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id, string? adminNotes)
        {
            var req = await _context.tblUnitTransferRequests
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.TransferID == id);
            if (req == null) return NotFound();
            if (req.Status != "Pending")
            {
                TempData["Error"] = "This request has already been reviewed.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _context.tblUnits.FindAsync(req.RequestedUnitID);
            if (target == null || !await UnitHasRoomAsync(target))
            {
                TempData["Error"] = "The requested unit is no longer available. The request was not approved.";
                return RedirectToAction(nameof(Index));
            }

            var tenant = req.Tenant!;
            int? oldUnitID = tenant.UnitID;                 // null when this is a first-unit application
            bool isApplication = oldUnitID == null;
            tenant.UnitID = req.RequestedUnitID;

            req.Status = "Approved";
            req.DateReviewed = DateTime.Now;
            req.AdminNotes = adminNotes;

            await _context.SaveChangesAsync();

            // refresh occupancy on the old (if any) and the new unit
            if (oldUnitID.HasValue) await UnitStatusHelper.RefreshAsync(_context, oldUnitID.Value);
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            string verb = isApplication ? "assigned to" : "moved to";
            if (tenant.UserID != null)
                await NotificationHelper.CreateAsync(_context, tenant.UserID.Value, "Transfer",
                    $"Your unit request was approved — you've been {verb} unit {target.UnitNumber}.", "/Transfers", req.TransferID);

            var approverId = CurrentUserID();
            if (approverId != null) await NotificationHelper.MarkRecordReadAsync(_context, approverId.Value, "Transfer", req.TransferID);

            TempData["Success"] = $"{tenant.FullName} was {verb} unit {target.UnitNumber}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string? adminNotes)
        {
            var req = await _context.tblUnitTransferRequests.FindAsync(id);
            if (req == null) return NotFound();
            if (req.Status != "Pending")
            {
                TempData["Error"] = "This request has already been reviewed.";
                return RedirectToAction(nameof(Index));
            }

            req.Status = "Rejected";
            req.DateReviewed = DateTime.Now;
            req.AdminNotes = adminNotes;
            await _context.SaveChangesAsync();

            // release the reservation on the requested unit
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            var rejectedTenant = await _context.tblTenants.FindAsync(req.TenantID);
            if (rejectedTenant?.UserID != null)
                await NotificationHelper.CreateAsync(_context, rejectedTenant.UserID.Value, "Transfer",
                    "Your unit transfer request was rejected." + (string.IsNullOrEmpty(adminNotes) ? "" : $" Note: {adminNotes}"), "/Transfers", req.TransferID);

            var reviewerId = CurrentUserID();
            if (reviewerId != null) await NotificationHelper.MarkRecordReadAsync(_context, reviewerId.Value, "Transfer", req.TransferID);

            TempData["Success"] = "Transfer request rejected.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> HasPendingAsync(int tenantId) =>
            await _context.tblUnitTransferRequests.AnyAsync(r => r.TenantID == tenantId && r.Status == "Pending");

        // a unit can take the tenant if it isn't full and isn't under maintenance
        private async Task<bool> UnitHasRoomAsync(tblUnit unit)
        {
            if (unit.Status == "Under Maintenance") return false;
            int active = await _context.tblTenants.CountAsync(t => t.UnitID == unit.UnitID && t.Status == "Active");
            return active < unit.Capacity;
        }

        // only truly Available units are offered — Reserved / Occupied ones are excluded
        private async Task PopulateAvailableUnitsAsync(tblTenant tenant)
        {
            var currentUnitId = tenant.UnitID;   // null when the tenant has no unit yet
            var units = await _context.tblUnits
                .Where(u => (currentUnitId == null || u.UnitID != currentUnitId) && u.Status == "Available")
                .OrderBy(u => u.UnitNumber)
                .ToListAsync();

            var options = new List<SelectListItem>();
            foreach (var u in units)
            {
                int active = await _context.tblTenants.CountAsync(t => t.UnitID == u.UnitID && t.Status == "Active");
                if (active < u.Capacity)
                    options.Add(new SelectListItem
                    {
                        Value = u.UnitID.ToString(),
                        Text = $"Unit {u.UnitNumber} — {u.UnitType} (₱{u.RentPrice:N0}/mo, {u.Capacity - active} slot(s) open)"
                    });
            }
            ViewBag.AvailableUnits = options;
        }
    }
}
