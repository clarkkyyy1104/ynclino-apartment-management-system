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

            // the property came online at the start of 2023; every sample date is drawn
            // from that window — 2023 up to the present — and never lands in the future
            var seedStart = new DateTime(2023, 1, 15);
            var unitEnd = now.AddDays(-20);
            double unitSpan = (unitEnd - seedStart).TotalDays;

            // 32 units, all 2-person rooms at ₱6,000/month (deposit + one-month advance
            // each equal to one month's rent). 22 will be filled by the 44 tenants below,
            // leaving 10 vacant so transfers and new registrations have somewhere to go.
            // "Date Added" climbs from early 2023 to a few weeks ago, with light jitter.
            var units = new List<tblUnit>();
            int number = 101;
            for (int i = 0; i < 32; i++)
            {
                var added = seedStart.AddDays(unitSpan * i / 31 + rng.Next(-4, 5));
                if (added > unitEnd) added = unitEnd;
                if (added < seedStart.AddDays(-6)) added = seedStart;
                units.Add(new tblUnit { UnitNumber = (number++).ToString(), UnitType = "Studio", RentPrice = 6000m, Deposit = 6000m, AdvancePayment = 6000m, Capacity = 2, Status = "Vacant", DateAdded = added });
            }
            _context.tblUnits.AddRange(units);
            await _context.SaveChangesAsync();

            // names chosen so every tenant's initials (and therefore username) are unique
            string[] firsts = { "Ana", "Ben", "Carlo", "Dina", "Elena", "Fidel", "Grace", "Hector", "Ivy", "Jose", "Karl", "Lara", "Marco", "Nina", "Oscar", "Paula", "Quennie", "Rico", "Sara", "Tomas", "Ulan", "Vera", "Wendell", "Xander", "Yana", "Zeny" };
            string[] lasts = { "Abad", "Bautista", "Cruz", "Diaz", "Espino", "Flores", "Garcia", "Hidalgo", "Ilagan", "Jimenez", "Katindig", "Lopez", "Mendoza", "Navarro", "Ocampo", "Perez", "Quizon", "Reyes", "Santos", "Torres", "Uy", "Valdez", "Wong", "Ximeno", "Yumul", "Zafra" };

            // 44 distinct (first-initial, last-initial) pairs
            var pairs = new List<(int f, int l)>();
            for (int k = 0; k < 26; k++) pairs.Add((k, k));               // AA, BB, ... ZZ
            for (int k = 0; pairs.Count < 44; k++) pairs.Add((k, k + 1)); // AB, BC, ...

            // fill the first 22 units (2 tenants each = 44), leaving the last 10 vacant
            var slots = new List<tblUnit>();
            for (int i = 0; i < 22; i++) for (int s = 0; s < 2; s++) slots.Add(units[i]);

            var createdTenants = new List<tblTenant>();
            for (int t = 0; t < 44; t++)
            {
                string first = firsts[pairs[t].f];
                string last = lasts[pairs[t].l];
                // registered sometime after their unit came online, up to the present,
                // so the username month (and the whole timeline) spans 2023 → now
                var unitAdded = slots[t].DateAdded;
                int regSpan = Math.Max(20, (int)(now - unitAdded).TotalDays - 3);
                var regDate = unitAdded.AddDays(rng.Next(3, regSpan));
                if (regDate > now.AddDays(-1)) regDate = now.AddDays(-1);
                // annual lease that has been renewed as needed, so it ends within the next year
                var leaseEnd = regDate.AddYears(1);
                while (leaseEnd < now) leaseEnd = leaseEnd.AddYears(1);
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

                var tenant = new tblTenant
                {
                    UserID = user.UserID,
                    UnitID = slots[t].UnitID,
                    FirstName = first,
                    LastName = last,
                    ContactNumber = "09171234567",
                    EmergencyContactName = "Guardian " + last,
                    EmergencyContactRelationship = "Parent",
                    EmergencyContactNumber = "09181234567",
                    MoveInDate = regDate,
                    LeaseStart = leaseEnd.AddYears(-1),
                    LeaseEnd = leaseEnd,
                    Status = "Active",
                    DateRecorded = regDate
                };
                _context.tblTenants.Add(tenant);
                createdTenants.Add(tenant);
            }
            await _context.SaveChangesAsync();

            // mark the fully-filled units Occupied; the rest stay Vacant
            foreach (var unit in units)
            {
                int active = await _context.tblTenants.CountAsync(t => t.UnitID == unit.UnitID && t.Status == "Active");
                unit.Status = active >= unit.Capacity ? "Occupied" : "Vacant";
            }
            await _context.SaveChangesAsync();

            // ── Connected transactions ──────────────────────────────────────
            // so the demo has real history to browse: past bills, maintenance
            // requests, and lost & found items tied to the sample tenants.
            var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);
            foreach (var (tenant, idx) in createdTenants.Select((t, i) => (t, i)))
            {
                // one bill per completed month the tenant has lived here, capped at the
                // last 6 so the history reads real without ballooning the table; older
                // months are paid, the most recent is paid for most and overdue for the rest
                var moveMonth = new DateTime(tenant.MoveInDate!.Value.Year, tenant.MoveInDate.Value.Month, 1);
                int monthsHere = ((firstOfThisMonth.Year - moveMonth.Year) * 12) + firstOfThisMonth.Month - moveMonth.Month;
                int months = Math.Min(6, monthsHere);
                for (int m = months; m >= 1; m--)
                {
                    var period = firstOfThisMonth.AddMonths(-m);
                    var due = period.AddDays(9);           // due on the 10th
                    bool paid = m >= 2 || (idx % 5 != 0);  // ~80% of last month paid
                    var bill = new tblBilling
                    {
                        TenantID = tenant.TenantID,
                        BillingPeriod = period,
                        AmountDue = 6000m,
                        DueDate = due,
                        DateIssued = period
                    };
                    if (paid)
                    {
                        bill.Status = "Paid";
                        bill.AmountPaid = 6000m;
                        bill.DatePaid = due.AddDays(-rng.Next(0, 6));
                    }
                    else
                    {
                        bill.Status = due < now ? "Overdue" : "Unpaid";
                    }
                    _context.tblBillings.Add(bill);
                }
            }

            // maintenance requests across a mix of statuses/priorities
            string[] mCats = { "Plumbing", "Electrical", "Structural", "Appliance", "Other" };
            string[] mPrio = { "Low", "Medium", "High", "Urgent" };
            string Describe(string c) => c switch
            {
                "Plumbing"   => "Leaking faucet in the bathroom.",
                "Electrical" => "Flickering lights in the room.",
                "Structural" => "Crack on the wall near the window.",
                "Appliance"  => "Air-conditioner is not cooling properly.",
                _            => "General upkeep request."
            };
            for (int i = 0; i < 14; i++)
            {
                var tenant = createdTenants[rng.Next(createdTenants.Count)];
                var cat = mCats[i % mCats.Length];
                var status = (i % 5) switch { 0 => "Pending", 1 => "In Progress", 4 => "Cancelled", _ => "Resolved" };
                // submitted sometime during this tenant's stay (so it never predates move-in)
                var moveIn = tenant.MoveInDate ?? now.AddMonths(-6);
                int daysHere = Math.Max(15, (int)(now - moveIn).TotalDays);
                var submitted = moveIn.AddDays(rng.Next(10, daysHere));
                if (submitted > now.AddDays(-1)) submitted = now.AddDays(-1);
                var resolved = submitted.AddDays(rng.Next(1, 10));
                if (resolved > now) resolved = now;
                _context.tblMaintenanceRequests.Add(new tblMaintenanceRequest
                {
                    TenantID = tenant.TenantID,
                    Category = cat,
                    Description = Describe(cat),
                    Priority = mPrio[i % mPrio.Length],
                    Status = status,
                    DateSubmitted = submitted,
                    DateResolved = status == "Resolved" ? resolved : (DateTime?)null,
                    AdminNotes = status == "Resolved" ? "Handled by maintenance staff." : null
                });
            }

            // lost & found: "Found" items are logged by the admin (front desk) so any
            // tenant can file a claim on them; "Lost" items are reported by tenants
            var adminId = (await _context.tblUsers.FirstOrDefaultAsync(u => u.Role == "Admin"))?.UserID
                          ?? createdTenants[0].UserID!.Value;

            var foundItems = new List<tblLostFoundItem>
            {
                new() { ReportedByUserID = adminId, ItemName = "Black Leather Wallet", ItemType = "Found", Location = "Lobby",        Status = "Reported", Description = "Found near the front desk.", DateReported = now.AddDays(-6) },
                new() { ReportedByUserID = adminId, ItemName = "iPhone 13 (blue case)", ItemType = "Found", Location = "2nd floor hall", Status = "Reported", Description = "Turned in by a resident.",    DateReported = now.AddDays(-4) },
                new() { ReportedByUserID = adminId, ItemName = "Silver House Keys",     ItemType = "Found", Location = "Parking area", Status = "Reported", Description = "Set of three keys on a ring.",  DateReported = now.AddDays(-2) },
                new() { ReportedByUserID = adminId, ItemName = "Umbrella (red)",         ItemType = "Found", Location = "Stairwell",    Status = "Resolved", Description = "Claimed and returned.",       DateReported = now.AddDays(-20) },
            };
            var lostItems = new List<tblLostFoundItem>
            {
                new() { ReportedByUserID = createdTenants[3].UserID!.Value,  ItemName = "Student ID Card",   ItemType = "Lost", Location = "Around the building", Status = "Reported", Description = "Lost my school ID.",       DateReported = now.AddDays(-5) },
                new() { ReportedByUserID = createdTenants[8].UserID!.Value,  ItemName = "Laptop Charger",    ItemType = "Lost", Location = "Study area",         Status = "Reported", Description = "65W USB-C charger.",       DateReported = now.AddDays(-3) },
                new() { ReportedByUserID = createdTenants[15].UserID!.Value, ItemName = "Silver Ring",       ItemType = "Lost", Location = "Laundry room",       Status = "Resolved", Description = "Already recovered.",       DateReported = now.AddDays(-25) },
            };
            _context.tblLostFoundItems.AddRange(foundItems);
            _context.tblLostFoundItems.AddRange(lostItems);
            await _context.SaveChangesAsync();

            // a couple of ownership claims on the found items (one pending, one approved)
            _context.tblClaimRequests.Add(new tblClaimRequest
            {
                ItemID = foundItems[0].ItemID,
                ClaimantUserID = createdTenants[5].UserID!.Value,
                VerificationDetails = "It's my wallet — brown card holder inside with my ID.",
                Status = "Pending",
                SubmittedAt = now.AddDays(-3)
            });
            _context.tblClaimRequests.Add(new tblClaimRequest
            {
                ItemID = foundItems[1].ItemID,
                ClaimantUserID = createdTenants[9].UserID!.Value,
                VerificationDetails = "That's my phone, lock screen is a photo of a dog.",
                Status = "Pending",
                SubmittedAt = now.AddDays(-1)
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Loaded 32 units and 44 tenants with sample bills, maintenance requests, and lost & found items. Sample tenants log in with password 'Tenant@123'.";
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
            unit.AdvancePayment = vm.AdvancePayment;
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
