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
    [Authorize(Roles = "Admin,Tenant")]
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
                .Include(t => t.Assignments).ThenInclude(a => a.Unit)
                .FirstOrDefaultAsync(t => t.UserID == uid && t.Status == "Active");
        }

        // A request may only be filed away once it is genuinely finished. 'Approved'
        // is NOT finished — the tenant still has to be moved — so it is deliberately
        // absent here. Archiving an approved transfer would hide outstanding work.
        private static readonly string[] ClosedStatuses = { "Completed", "Rejected", "Cancelled" };

        // Archiving is per side: the tenant clears their own list without touching
        // the admin's, and the other way round.
        private bool ViewingAsTenant() => User.IsInRole("Tenant");

        // GET: Transfers — one list at a time, the active one or the archive,
        // the same way Billing and Maintenance do it.
        public async Task<IActionResult> Index(bool archived = false)
        {
            IQueryable<tblUnitTransferRequest> query = _context.tblUnitTransferRequests
                .Include(r => r.Tenant).ThenInclude(t => t!.Assignments).ThenInclude(a => a.Unit)
                .Include(r => r.CurrentUnit)
                .Include(r => r.RequestedUnit);

            if (User.IsInRole("Tenant"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null)
                {
                    ViewBag.Archived = archived;
                    return View(new List<tblUnitTransferRequest>());
                }
                query = query.Where(r => r.TenantID == tenant.TenantID);
                ViewBag.HasUnit = tenant.Assignments.Any(a => a.Status == "Active");   // drives "Apply for a Unit" vs "Request Transfer"
            }

            var all = await query.ToListAsync();

            // A reviewed request stays in Active until THIS side archives it.
            bool asTenant = ViewingAsTenant();
            bool IsFiled(tblUnitTransferRequest r) =>
                asTenant ? r.TenantArchivedAt != null : r.StaffArchivedAt != null;

            var shown = archived
                ? all.Where(IsFiled).OrderByDescending(r => r.DateReviewed).ToList()
                : all.Where(r => !IsFiled(r)).OrderByDescending(r => r.DateRequested).ToList();

            ViewBag.Archived = archived;
            ViewBag.ClosedStatuses = ClosedStatuses;

            return View(shown);
        }

        // POST: Transfers/Archive/5 — file a reviewed request away, for MY side only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var req = await _context.tblUnitTransferRequests.FindAsync(id);
            if (req == null) return NotFound();
            if (!await CanTouchAsync(req)) return Forbid();

            // a pending request is still waiting on the admin — it stays in Active
            if (!ClosedStatuses.Contains(req.Status))
            {
                TempData["Error"] = "A pending request cannot be archived.";
                return RedirectToAction(nameof(Index));
            }

            if (ViewingAsTenant()) req.TenantArchivedAt = DateTime.Now;
            else                   req.StaffArchivedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Request moved to your archive.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/Unarchive/5 — pull it back into MY active list
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id)
        {
            var req = await _context.tblUnitTransferRequests.FindAsync(id);
            if (req == null) return NotFound();
            if (!await CanTouchAsync(req)) return Forbid();

            if (ViewingAsTenant()) req.TenantArchivedAt = null;
            else                   req.StaffArchivedAt = null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Request restored to your active list.";
            // you were reading the archive, so that is where you stay
            return RedirectToAction(nameof(Index), new { archived = true });
        }

        // a tenant may only archive their own request
        private async Task<bool> CanTouchAsync(tblUnitTransferRequest req)
        {
            if (User.IsInRole("Admin")) return true;
            var tenant = await GetCurrentTenantAsync();
            return tenant != null && req.TenantID == tenant.TenantID;
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
                TempData["Error"] = await OpenRequestMessageAsync(tenant.TenantID);
                return RedirectToAction(nameof(Index));
            }

            await PopulateAvailableUnitsAsync(tenant);
            ViewBag.CurrentUnit = tenant.Unit?.UnitNumber;
            ViewBag.HasUnit = tenant.Assignments.Any(a => a.Status == "Active");
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
                TempData["Error"] = await OpenRequestMessageAsync(tenant.TenantID);
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
                ViewBag.HasUnit = tenant.Assignments.Any(a => a.Status == "Active");
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

        // POST: Transfers/Approve/5
        // Step 2 of the flow: the admin AGREES to the move. Nothing is moved here.
        // The request becomes "subject for transfer" and waits for Complete, which
        // is where the tenant's assignment actually changes. Keeping the decision
        // and the move apart is what lets the office record that a transfer was
        // agreed on one day and carried out on another.
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

            var problem = await TransferBlockedReasonAsync(req, "approved");
            if (problem != null)
            {
                TempData["Error"] = problem;
                return RedirectToAction(nameof(Index));
            }

            req.Status = "Approved";
            req.DateReviewed = DateTime.Now;
            req.AdminNotes = adminNotes;
            await _context.SaveChangesAsync();

            // The unit was already Reserved while the request was Pending and it
            // stays Reserved now, so nobody else can take it before the move. The
            // refresh is here so the status is re-derived from the new state rather
            // than assumed.
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            var unitNo = (await _context.tblUnits.FindAsync(req.RequestedUnitID))?.UnitNumber;
            TempData["Success"] = $"Request approved. {req.Tenant!.FullName} is now subject for transfer " +
                                  $"to unit {unitNo}. Mark it as completed once they have actually moved.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/Complete/5
        // Step 3 of the flow: the admin confirms the tenant HAS moved. This is the
        // only place the tenant's unit changes — setting tenant.UnitID closes the
        // current TenantUnitAssignment and opens a new one, so the tenancy history
        // records the move instead of overwriting it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Complete(int id, string? adminNotes)
        {
            var req = await _context.tblUnitTransferRequests
                .Include(r => r.Tenant).ThenInclude(t => t!.Assignments)
                .FirstOrDefaultAsync(r => r.TransferID == id);
            if (req == null) return NotFound();
            if (req.Status != "Approved")
            {
                TempData["Error"] = req.Status == "Pending"
                    ? "This request has to be approved before it can be completed."
                    : "Only an approved request can be marked as completed.";
                return RedirectToAction(nameof(Index));
            }

            // Time has passed since the approval, so every check made then has to be
            // made again. An approval is a promise, not a guarantee: the unit may
            // have filled up and the tenant may have been deactivated in between.
            var problem = await TransferBlockedReasonAsync(req, "completed");
            if (problem != null)
            {
                TempData["Error"] = problem;
                return RedirectToAction(nameof(Index));
            }

            var target = (await _context.tblUnits.FindAsync(req.RequestedUnitID))!;
            var tenant = req.Tenant!;
            int? oldUnitID = tenant.UnitID;                 // null when this is a first-unit application
            bool isApplication = oldUnitID == null;

            tenant.UnitID = req.RequestedUnitID;            // the move itself

            req.Status = "Completed";
            req.DateCompleted = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(adminNotes)) req.AdminNotes = adminNotes;

            await _context.SaveChangesAsync();

            // refresh occupancy on the old (if any) and the new unit
            if (oldUnitID.HasValue) await UnitStatusHelper.RefreshAsync(_context, oldUnitID.Value);
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            string verb = isApplication ? "assigned to" : "moved to";
            TempData["Success"] = $"{tenant.FullName} was {verb} unit {target.UnitNumber}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Transfers/CancelApproved/5
        // An approved move that never happened — the tenant changed their mind, or
        // the office called it off. This releases the unit it was holding.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CancelApproved(int id, string? adminNotes)
        {
            var req = await _context.tblUnitTransferRequests.FindAsync(id);
            if (req == null) return NotFound();
            if (req.Status != "Approved")
            {
                TempData["Error"] = "Only an approved request that has not been completed can be called off.";
                return RedirectToAction(nameof(Index));
            }

            req.Status = "Cancelled";
            if (!string.IsNullOrWhiteSpace(adminNotes)) req.AdminNotes = adminNotes;
            await _context.SaveChangesAsync();

            // the unit is no longer being held for this tenant
            await UnitStatusHelper.RefreshAsync(_context, req.RequestedUnitID);
            await _context.SaveChangesAsync();

            TempData["Success"] = "The approved transfer was called off and the unit released.";
            return RedirectToAction(nameof(Index));
        }

        // The conditions a transfer needs in order to go ahead, checked both when it
        // is approved and again when it is completed. Returns null when it is fine,
        // or the reason it cannot proceed. 'stage' only shapes the wording.
        private async Task<string?> TransferBlockedReasonAsync(tblUnitTransferRequest req, string stage)
        {
            var target = await _context.tblUnits.FindAsync(req.RequestedUnitID);
            if (target == null || !await UnitHasRoomAsync(target))
                return $"The requested unit is no longer available, so the request was not {stage}.";

            // Load the tenant WITH their assignments, always. tenant.UnitID reads the
            // Active row out of Assignments, and lazy loading is off in this project,
            // so an un-Included collection makes UnitID silently null — which would
            // read as "the tenant has been moved" and block every genuine transfer.
            // EF returns the already-tracked instance here, so this also fills in
            // req.Tenant.Assignments for the caller.
            var tenant = await _context.tblTenants
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.TenantID == req.TenantID);
            if (tenant == null) return "The tenant on this request no longer exists.";

            if (tenant.Status != "Active")
                return $"That tenant is no longer active, so the request cannot be {stage}.";
            if (tenant.UnitID == req.RequestedUnitID)
                return "That tenant is already in the requested unit.";
            if (tenant.UnitID != req.CurrentUnitID)
                return $"The tenant has been moved since this request was filed, so it cannot be {stage}. " +
                       "Review the request against their current unit.";

            return null;
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

            TempData["Success"] = "Transfer request rejected.";
            return RedirectToAction(nameof(Index));
        }

        // One open request at a time. 'Approved' counts as open: the tenant has been
        // promised a unit and has not moved into it yet, so they must not be able to
        // queue up a second request against a different one.
        private async Task<bool> HasPendingAsync(int tenantId) =>
            await _context.tblUnitTransferRequests.AnyAsync(r =>
                r.TenantID == tenantId && (r.Status == "Pending" || r.Status == "Approved"));

        // "You already have a request" means two different things now, so say which.
        // An approved one has been decided and is waiting on the office, not on them.
        private async Task<string> OpenRequestMessageAsync(int tenantId)
        {
            bool approved = await _context.tblUnitTransferRequests
                .AnyAsync(r => r.TenantID == tenantId && r.Status == "Approved");

            return approved
                ? "Your unit request has already been approved and is waiting to be carried out. " +
                  "Please contact the office about the move instead of filing another request."
                : "You already have a pending unit request. Please wait for it to be reviewed.";
        }

        // a unit can take the tenant if it isn't full and isn't under maintenance
        private async Task<bool> UnitHasRoomAsync(tblUnit unit)
        {
            if (unit.Status == "Under Maintenance") return false;
            int active = await _context.tblTenants.CountAsync(t => t.Assignments.Any(a => a.Status == "Active" && a.UnitID == unit.UnitID) && t.Status == "Active");
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
                int active = await _context.tblTenants.CountAsync(t => t.Assignments.Any(a => a.Status == "Active" && a.UnitID == u.UnitID) && t.Status == "Active");
                if (active < u.Capacity)
                    options.Add(new SelectListItem
                    {
                        Value = u.UnitID.ToString(),
                        Text = $"Unit {u.UnitNumber} — {u.UnitType} (₱{u.RentPrice:N2}/mo, {u.Capacity - active} slot(s) open)"
                    });
            }
            ViewBag.AvailableUnits = options;
        }
    }
}
