using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
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

        // reviewed requests move out of the active list into the archive
        private static readonly string[] ArchivedStatuses = { "Approved", "Rejected" };

        // GET: Transfers
        public async Task<IActionResult> Index(bool archived = false)
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
                    ViewBag.Archived = archived;
                    return View(new List<tblUnitTransferRequest>());
                }
                query = query.Where(r => r.TenantID == tenant.TenantID);
            }

            // the archive holds reviewed requests; the main list holds pending ones
            if (archived)
                query = query.Where(r => ArchivedStatuses.Contains(r.Status));
            else
                query = query.Where(r => !ArchivedStatuses.Contains(r.Status));

            var list = await query
                .OrderByDescending(r => archived ? r.DateReviewed : r.DateRequested)
                .ToListAsync();
            ViewBag.Archived = archived;
            return View(list);
        }

        // GET: Transfers/Create?unitId=5  (tenant confirms a move to a vacant unit
        // they picked from the Units page)
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create(int? unitId)
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

            if (unitId == null)
            {
                TempData["Error"] = "Choose a vacant unit from the Units page to request a transfer.";
                return RedirectToAction("Index", "Units");
            }

            var target = await _context.tblUnits.FindAsync(unitId.Value);
            if (target == null || target.UnitID == tenant.UnitID || !await UnitHasRoomAsync(target))
            {
                TempData["Error"] = "That unit isn't available for a transfer. Please pick another vacant unit.";
                return RedirectToAction("Index", "Units");
            }

            ViewBag.CurrentUnit = tenant.Unit?.UnitNumber;
            return View(target);
        }

        // POST: Transfers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create(int requestedUnitID, string reason)
        {
            var tenant = await GetCurrentTenantAsync();
            if (tenant == null) return Forbid();

            if (await HasPendingAsync(tenant.TenantID))
            {
                TempData["Error"] = "You already have a pending transfer request.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _context.tblUnits.FindAsync(requestedUnitID);
            if (target == null || target.UnitID == tenant.UnitID || !await UnitHasRoomAsync(target))
            {
                TempData["Error"] = "That unit is no longer available. Please pick another vacant unit.";
                return RedirectToAction("Index", "Units");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                ModelState.AddModelError("reason", "Please give a reason for the transfer.");
                ViewBag.CurrentUnit = tenant.Unit?.UnitNumber;
                return View(target);
            }

            _context.tblUnitTransferRequests.Add(new tblUnitTransferRequest
            {
                TenantID = tenant.TenantID,
                CurrentUnitID = tenant.UnitID,
                RequestedUnitID = requestedUnitID,
                Reason = reason.Trim(),
                Status = "Pending",
                DateRequested = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Your unit transfer request has been submitted.";
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
            int oldUnitID = tenant.UnitID;
            tenant.UnitID = req.RequestedUnitID;

            req.Status = "Approved";
            req.DateReviewed = DateTime.Now;
            req.AdminNotes = adminNotes;

            await _context.SaveChangesAsync();

            // refresh occupancy on both units after the move
            await RefreshUnitStatusAsync(oldUnitID);
            await RefreshUnitStatusAsync(req.RequestedUnitID);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{tenant.FullName} was moved to unit {target.UnitNumber}.";
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

        private async Task RefreshUnitStatusAsync(int unitID)
        {
            var unit = await _context.tblUnits.FindAsync(unitID);
            if (unit == null || unit.Status == "Under Maintenance") return;
            int active = await _context.tblTenants.CountAsync(t => t.UnitID == unitID && t.Status == "Active");
            unit.Status = active >= unit.Capacity ? "Occupied" : "Vacant";
        }
    }
}
