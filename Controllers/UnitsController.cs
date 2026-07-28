using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
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
            ViewBag.HasUnits = await _context.tblUnits.AnyAsync();

            var units = await query.OrderBy(u => u.UnitNumber).ToListAsync();
            return View(units);
        }

        // GET: Units/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var unit = await _context.tblUnits
                .Include(u => u.Tenants)
                .FirstOrDefaultAsync(u => u.UnitID == id);

            if (unit == null) return NotFound();
            return View(unit);
        }

        // GET: Units/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            return View(new UnitViewModel { UnitNumber = await NextUnitNumberAsync() });
        }

        // POST: Units/GenerateSampleData
        // one-time loader for the survey data: 22 units (13 bedspacers, 9 studios)
        // and 44 tenants, created through the same logic the forms use.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateSampleData()
        {
            if (await _context.tblUnits.AnyAsync())
            {
                TempData["Error"] = "Sample data was not loaded because units already exist.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var rng = new Random();

            // random past date for tenants (never today)
            DateTime RandomPastDate() => now.AddDays(-rng.Next(30, 730));

            // unit "Date Added" climbs with the unit number: the earliest unit is ~2.5
            // years back and each following unit is a little newer, all still in the past
            var addedDate = now.AddDays(-900);
            DateTime NextAddedDate()
            {
                var d = addedDate;
                addedDate = addedDate.AddDays(rng.Next(15, 41));
                return d;
            }

            // 22 units: 13 bedspacers (4 beds each), 9 studios (good for 2)
            var bedspacers = new List<tblUnit>();
            var studios = new List<tblUnit>();
            int number = 101;
            for (int i = 0; i < 13; i++)
                bedspacers.Add(new tblUnit { UnitNumber = (number++).ToString(), UnitType = "Bedspacer", RentPrice = 1300m, Deposit = 2600m, Capacity = 4, Status = "Vacant", DateAdded = NextAddedDate() });
            for (int i = 0; i < 9; i++)
                studios.Add(new tblUnit { UnitNumber = (number++).ToString(), UnitType = "Studio", RentPrice = 3000m, Deposit = 6000m, Capacity = 2, Status = "Vacant", DateAdded = NextAddedDate() });
            _context.tblUnits.AddRange(bedspacers);
            _context.tblUnits.AddRange(studios);
            await _context.SaveChangesAsync();

            // names chosen so every tenant's initials (and therefore username) are unique
            string[] firsts = { "Ana", "Ben", "Carlo", "Dina", "Elena", "Fidel", "Grace", "Hector", "Ivy", "Jose", "Karl", "Lara", "Marco", "Nina", "Oscar", "Paula", "Quennie", "Rico", "Sara", "Tomas", "Ulan", "Vera", "Wendell", "Xander", "Yana", "Zeny" };
            string[] lasts = { "Abad", "Bautista", "Cruz", "Diaz", "Espino", "Flores", "Garcia", "Hidalgo", "Ilagan", "Jimenez", "Katindig", "Lopez", "Mendoza", "Navarro", "Ocampo", "Perez", "Quizon", "Reyes", "Santos", "Torres", "Uy", "Valdez", "Wong", "Ximeno", "Yumul", "Zafra" };

            // 44 distinct (first-initial, last-initial) pairs
            var pairs = new List<(int f, int l)>();
            for (int k = 0; k < 26; k++) pairs.Add((k, k));               // AA, BB, ... ZZ
            for (int k = 0; pairs.Count < 44; k++) pairs.Add((k, k + 1)); // AB, BC, ...

            // where each tenant goes: fill 10 bedspacers (x4) and 2 studios (x2) = 44,
            // leaving 3 bedspacers and 7 studios open
            var slots = new List<tblUnit>();
            for (int i = 0; i < 10; i++) for (int s = 0; s < 4; s++) slots.Add(bedspacers[i]);
            for (int i = 0; i < 2; i++) for (int s = 0; s < 2; s++) slots.Add(studios[i]);

            for (int t = 0; t < 44; t++)
            {
                string first = firsts[pairs[t].f];
                string last = lasts[pairs[t].l];
                // a random past registration date so the username month varies too
                var regDate = RandomPastDate();
                string username = $"{regDate:yy}-{regDate:MM}{char.ToUpper(first[0])}{char.ToUpper(last[0])}";

                var user = new tblUser
                {
                    Username = username,
                    Password = PasswordHelper.Hash("Tenant@123"),
                    Role = "Tenant",
                    IsActive = true,
                    IsMainAdmin = false,
                    DateCreated = regDate
                };
                _context.tblUsers.Add(user);
                await _context.SaveChangesAsync();

                _context.tblTenants.Add(new tblTenant
                {
                    UserID = user.UserID,
                    UnitID = slots[t].UnitID,
                    FirstName = first,
                    LastName = last,
                    ContactNumber = "09171234567",
                    EmergencyContactName = "Guardian " + last,
                    EmergencyContactRelationship = "Parent",
                    EmergencyContactNumber = "09181234567",
                    Status = "Active",
                    DateRecorded = regDate
                });
            }
            await _context.SaveChangesAsync();

            // mark the fully-filled units Occupied; the rest stay Vacant
            foreach (var unit in bedspacers.Concat(studios))
            {
                int active = await _context.tblTenants.CountAsync(t => t.UnitID == unit.UnitID && t.Status == "Active");
                unit.Status = active >= unit.Capacity ? "Occupied" : "Vacant";
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "Loaded 22 units and 44 tenants. Sample tenants log in with password 'Tenant@123'.";
            return RedirectToAction(nameof(Index));
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
        [Authorize(Roles = "Admin")]
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
                Deposit = vm.Deposit,
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
            if (!ModelState.IsValid) return View(vm);

            bool duplicate = await _context.tblUnits.AnyAsync(u => u.UnitNumber == vm.UnitNumber && u.UnitID != id);
            if (duplicate)
            {
                ModelState.AddModelError("UnitNumber", "Unit Number already exists.");
                return View(vm);
            }

            // keep the status honest against actual tenancy — "Occupied" means full,
            // so a partially filled unit legitimately stays Vacant
            int activeTenants = await _context.tblTenants.CountAsync(t => t.UnitID == id && t.Status == "Active");
            if (vm.Status == "Vacant" && activeTenants >= vm.Capacity)
            {
                ModelState.AddModelError("Status", "This unit is at full capacity. Move a tenant out before marking it Vacant.");
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
            unit.Capacity = vm.Capacity;
            unit.Status = vm.Status;

            // a capacity change can flip whether the unit counts as full,
            // so re-derive Vacant/Occupied unless it's under maintenance
            if (unit.Status != "Under Maintenance")
                unit.Status = activeTenants >= unit.Capacity ? "Occupied" : "Vacant";

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
                .Include(u => u.Tenants)
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
            var unit = await _context.tblUnits.Include(u => u.Tenants).FirstOrDefaultAsync(u => u.UnitID == id);
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

            _context.tblUnits.Remove(unit);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Unit {unit.UnitNumber} has been removed.";
            return RedirectToAction(nameof(Index));
        }
    }
}
