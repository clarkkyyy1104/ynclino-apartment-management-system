using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;
using YnclinoAMS.Models;
using YnclinoAMS.Models.ViewModels;

namespace YnclinoAMS.Controllers
{
    [Authorize]
    public class LostFoundController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LostFoundController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // GET: LostFound
        public async Task<IActionResult> Index(string? typeFilter, string? statusFilter, string? searchTerm)
        {
            IQueryable<tblLostFoundItem> query = _context.tblLostFoundItems
                .Include(l => l.ReportedBy);

            // Tenants only see their own reports
            if (User.IsInRole("Tenant"))
            {
                var uid = CurrentUserID();
                query = query.Where(l => l.ReportedByUserID == uid);
            }

            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(l => l.ItemType == typeFilter);

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(l => l.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(l =>
                    l.ItemName.Contains(searchTerm) ||
                    (l.Location != null && l.Location.Contains(searchTerm)));

            ViewBag.TypeFilter   = typeFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm   = searchTerm;

            return View(await query.OrderByDescending(l => l.DateReported).ToListAsync());
        }

        // GET: LostFound/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems
                .Include(l => l.ReportedBy)
                .FirstOrDefaultAsync(l => l.ItemID == id);

            if (item == null) return NotFound();

            if (User.IsInRole("Tenant") && item.ReportedByUserID != CurrentUserID())
                return Forbid();

            return View(item);
        }

        // GET: LostFound/Create
        public IActionResult Create() => View(new LostFoundViewModel());

        // POST: LostFound/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LostFoundViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var uid = CurrentUserID();
            if (uid == null) return Forbid();

            var item = new tblLostFoundItem
            {
                ReportedByUserID = uid.Value,
                ItemName         = vm.ItemName,
                Description      = vm.Description,
                ItemType         = vm.ItemType,
                Location         = vm.Location,
                Status           = "Reported",
                DateReported     = DateTime.Now,
                Notes            = vm.Notes
            };

            _context.tblLostFoundItems.Add(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{vm.ItemType} item \"{vm.ItemName}\" has been reported.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems
                .Include(l => l.ReportedBy)
                .FirstOrDefaultAsync(l => l.ItemID == id);

            if (item == null) return NotFound();

            var vm = new LostFoundViewModel
            {
                ItemID           = item.ItemID,
                ReportedByUserID = item.ReportedByUserID,
                ReportedByName   = item.ReportedBy?.Username,
                ItemName         = item.ItemName,
                Description      = item.Description,
                ItemType         = item.ItemType,
                Location         = item.Location,
                Status           = item.Status,
                DateReported     = item.DateReported,
                Notes            = item.Notes
            };
            return View(vm);
        }

        // POST: LostFound/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LostFoundViewModel vm)
        {
            if (id != vm.ItemID) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            item.ItemName    = vm.ItemName;
            item.Description = vm.Description;
            item.ItemType    = vm.ItemType;
            item.Location    = vm.Location;
            item.Status      = vm.Status;
            item.Notes       = vm.Notes;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Item record updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems
                .Include(l => l.ReportedBy)
                .FirstOrDefaultAsync(l => l.ItemID == id);

            if (item == null) return NotFound();
            return View(item);
        }

        // POST: LostFound/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            _context.tblLostFoundItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Item record deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
