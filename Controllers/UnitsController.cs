using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Tenant")]
    public class UnitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Units
        public async Task<IActionResult> Index(string? statusFilter, string? searchTerm)
        {
            var query = _context.tblUnits.Include(u => u.Assignments).ThenInclude(a => a.Tenant).AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(u => u.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(u => u.UnitNumber.Contains(searchTerm) || u.UnitType.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

            var units = await query.OrderBy(u => u.UnitNumber).ToListAsync();
            return View(units);
        }

        // GET: Units/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits
                .Include(u => u.Assignments).ThenInclude(a => a.Tenant)
                .FirstOrDefaultAsync(u => u.UnitID == id);

            if (unit == null) return NotFound();
            return View(unit);
        }

        // GET: Units/History/5 — who has lived in this unit, opened from its details
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> History(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits
                .Include(u => u.Assignments).ThenInclude(a => a.Tenant)
                .FirstOrDefaultAsync(u => u.UnitID == id);

            if (unit == null) return NotFound();
            ViewBag.Assignments = await _context.TenantUnitAssignments
                .Include(a => a.Tenant)
                .Where(a => a.UnitID == unit.UnitID)
                .OrderByDescending(a => a.AssignmentID)
                .ToListAsync();
            return View(unit);
        }

        // GET: Units/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            return View(new UnitViewModel { UnitNumber = await NextUnitNumberAsync() });
        }

        // suggests the next clean numeric unit number (highest existing + 1)
        private async Task<string> NextUnitNumberAsync()
        {
            var numbers = await _context.tblUnits
                .Select(u => u.UnitNumber)
                .ToListAsync();

            int max = numbers
                .Select(n => int.TryParse(n, out int v) ? v : 0)
                .DefaultIfEmpty(100)
                .Max();

            return (max + 1).ToString();
        }

        // The whole system treats the deposit and the advance as ONE MONTH each:
        // the move-in bill charges Deposit + AdvancePayment, and "Deposit on File"
        // is shown on every bill. A deposit larger than a month's rent is therefore
        // almost always a typo (a 13,000 deposit typed on a 3,000 unit), and it
        // quietly corrupts every bill that unit ever produces. Less than a month is
        // allowed — an admin may discount it — but more is refused.
        private void ValidateMoveInAmounts(UnitViewModel vm)
        {
            if (vm.RentPrice <= 0) return;   // the Required/Range rules already caught this

            if (vm.Deposit > vm.RentPrice)
                ModelState.AddModelError(nameof(vm.Deposit),
                    $"The deposit cannot be more than one month's rent (₱{vm.RentPrice:N0}).");

            if (vm.AdvancePayment > vm.RentPrice)
                ModelState.AddModelError(nameof(vm.AdvancePayment),
                    $"The advance cannot be more than one month's rent (₱{vm.RentPrice:N0}).");
        }

        // POST: Units/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(UnitViewModel vm)
        {
            ValidateMoveInAmounts(vm);
            if (!ModelState.IsValid) return View(vm);

            bool duplicate = await _context.tblUnits.AnyAsync(u => u.UnitNumber == vm.UnitNumber);
            if (duplicate)
            {
                ModelState.AddModelError("UnitNumber", "Unit Number already exists.");
                return View(vm);
            }

            var unit = new tblUnit
            {
                UnitNumber = vm.UnitNumber,
                UnitType = vm.UnitType,
                RentPrice = vm.RentPrice,
                Deposit = vm.Deposit,
                AdvancePayment = vm.AdvancePayment,
                Capacity = vm.Capacity,
                Status = vm.Status,
                DateAdded = DateTime.Now
            };

            _context.tblUnits.Add(unit);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Unit {unit.UnitNumber} has been added.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Units/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits.FindAsync(id);
            if (unit == null) return NotFound();

            var vm = new UnitViewModel
            {
                UnitID = unit.UnitID,
                UnitNumber = unit.UnitNumber,
                UnitType = unit.UnitType,
                RentPrice = unit.RentPrice,
                Deposit = unit.Deposit,
                AdvancePayment = unit.AdvancePayment,
                Capacity = unit.Capacity,
                Status = unit.Status
            };
            return View(vm);
        }

        // POST: Units/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, UnitViewModel vm)
        {
            if (id != vm.UnitID) return NotFound();
            ValidateMoveInAmounts(vm);
            if (!ModelState.IsValid) return View(vm);

            bool duplicate = await _context.tblUnits.AnyAsync(u => u.UnitNumber == vm.UnitNumber && u.UnitID != id);
            if (duplicate)
            {
                ModelState.AddModelError("UnitNumber", "Unit Number already exists.");
                return View(vm);
            }

            // keep the status honest against actual tenancy — "Occupied" means full,
            // so a partially filled unit legitimately stays Available
            int activeTenants = await _context.tblTenants.CountAsync(t => t.Assignments.Any(a => a.Status == "Active" && a.UnitID == id) && t.Status == "Active");
            if (vm.Status == "Available" && activeTenants >= vm.Capacity)
            {
                ModelState.AddModelError("Status", "This unit is at full capacity. Move a tenant out before marking it Available.");
                return View(vm);
            }
            if (vm.Status == "Occupied" && activeTenants == 0)
            {
                ModelState.AddModelError("Status", "This unit has no active tenant. Register a tenant to mark it occupied.");
                return View(vm);
            }

            var unit = await _context.tblUnits.FindAsync(id);
            if (unit == null) return NotFound();

            unit.UnitNumber = vm.UnitNumber;
            unit.UnitType = vm.UnitType;
            unit.RentPrice = vm.RentPrice;
            unit.Deposit = vm.Deposit;
            unit.AdvancePayment = vm.AdvancePayment;
            unit.Capacity = vm.Capacity;
            unit.Status = vm.Status;

            // a capacity change can flip whether the unit counts as full,
            // so re-derive Available/Occupied unless it's under maintenance
            if (unit.Status != "Under Maintenance")
                unit.Status = activeTenants >= unit.Capacity ? "Occupied" : "Available";

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Unit {unit.UnitNumber} has been updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.tblUnits.Any(u => u.UnitID == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Units/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits
                .Include(u => u.Assignments).ThenInclude(a => a.Tenant)
                .FirstOrDefaultAsync(u => u.UnitID == id);

            if (unit == null) return NotFound();
            return View(unit);
        }

        // POST: Units/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var unit = await _context.tblUnits.Include(u => u.Assignments).ThenInclude(a => a.Tenant).FirstOrDefaultAsync(u => u.UnitID == id);
            if (unit == null) return NotFound();

            if (unit.Tenants.Any(t => t.Status == "Active"))
            {
                TempData["Error"] = "Cannot delete a unit with active tenants.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            // past tenants keep a hard reference to the unit, so the delete would fail anyway
            if (unit.Tenants.Any())
            {
                TempData["Error"] = $"Unit {unit.UnitNumber} has tenancy history and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.TenantUnitAssignments.AnyAsync(a => a.UnitID == id) ||
                await _context.tblMaintenanceRequests.AnyAsync(m => m.UnitID == id) ||
                await _context.tblUnitTransferRequests.AnyAsync(r => r.RequestedUnitID == id || r.CurrentUnitID == id))
            {
                TempData["Error"] = $"Unit {unit.UnitNumber} has request or assignment history and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            _context.tblUnits.Remove(unit);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Unit {unit.UnitNumber} has been removed.";
            return RedirectToAction(nameof(Index));
        }
    }
}
