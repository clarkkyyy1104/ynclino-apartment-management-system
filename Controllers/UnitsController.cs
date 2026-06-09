using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize]
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
            var query = _context.tblUnits.Include(u => u.Tenants).AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(u => u.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(u => u.UnitNumber.Contains(searchTerm) || u.UnitType.Contains(searchTerm));

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

            var units = await query.OrderBy(u => u.UnitNumber).ToListAsync();
            return View(units);
        }

        // GET: Units/Create
        [Authorize(Roles = "Admin,SemiAdmin")]
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

        // POST: Units/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Create(UnitViewModel vm)
        {
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
        [Authorize(Roles = "Admin,SemiAdmin")]
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
                Capacity = unit.Capacity,
                Status = unit.Status
            };
            return View(vm);
        }

        // POST: Units/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Edit(int id, UnitViewModel vm)
        {
            if (id != vm.UnitID) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            bool duplicate = await _context.tblUnits.AnyAsync(u => u.UnitNumber == vm.UnitNumber && u.UnitID != id);
            if (duplicate)
            {
                ModelState.AddModelError("UnitNumber", "Unit Number already exists.");
                return View(vm);
            }

            var unit = await _context.tblUnits.FindAsync(id);
            if (unit == null) return NotFound();

            unit.UnitNumber = vm.UnitNumber;
            unit.UnitType = vm.UnitType;
            unit.RentPrice = vm.RentPrice;
            unit.Capacity = vm.Capacity;
            unit.Status = vm.Status;

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
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits
                .Include(u => u.Tenants)
                .FirstOrDefaultAsync(u => u.UnitID == id);

            if (unit == null) return NotFound();
            return View(unit);
        }

        // POST: Units/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var unit = await _context.tblUnits.Include(u => u.Tenants).FirstOrDefaultAsync(u => u.UnitID == id);
            if (unit == null) return NotFound();

            if (unit.Tenants.Any(t => t.Status == "Active"))
            {
                TempData["Error"] = "Cannot delete a unit with active tenants.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.tblUnits.Remove(unit);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Unit {unit.UnitNumber} has been removed.";
            return RedirectToAction(nameof(Index));
        }
    }
}
