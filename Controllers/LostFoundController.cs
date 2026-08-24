using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class LostFoundController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LostFoundController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? CurrentUserID() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;

        // GET: LostFound
        public async Task<IActionResult> Index(string? typeFilter, string? statusFilter, string? searchTerm)
        {
            IQueryable<tblLostFoundItem> query = _context.tblLostFoundItems;

            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(l => l.ItemType == typeFilter);

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(l => l.Status == statusFilter);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(l =>
                    l.ItemName.Contains(searchTerm) ||
                    (l.Location != null && l.Location.Contains(searchTerm)));

            ViewBag.TypeFilter = typeFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchTerm = searchTerm;

            return View(await query.OrderByDescending(l => l.DateReported).ToListAsync());
        }

        // GET: LostFound/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems.FirstOrDefaultAsync(l => l.ItemID == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // GET: LostFound/Create
        public IActionResult Create() => View(new LostFoundViewModel());

        // POST: LostFound/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LostFoundViewModel vm)
        {
            if (vm.ImageUpload != null && !ImageUploadHelper.IsValid(vm.ImageUpload, out var imgErr))
                ModelState.AddModelError(nameof(vm.ImageUpload), imgErr);

            if (!ModelState.IsValid) return View(vm);

            var uid = CurrentUserID();
            if (uid == null) return Forbid();

            string? imagePath = null;
            if (vm.ImageUpload != null)
                imagePath = await ImageUploadHelper.SaveAsync(vm.ImageUpload, "lostfound", _env);

            var item = new tblLostFoundItem
            {
                ReportedByUserID = uid.Value,
                ItemName = vm.ItemName,
                Description = vm.Description,
                ItemType = vm.ItemType,
                Location = vm.Location,
                Status = "Reported",
                DateReported = DateTime.Now,
                Notes = vm.Notes,
                ImagePath = imagePath
            };

            _context.tblLostFoundItems.Add(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{vm.ItemType} item \"{vm.ItemName}\" has been recorded.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            var vm = new LostFoundViewModel
            {
                ItemID = item.ItemID,
                ReportedByUserID = item.ReportedByUserID,
                ItemName = item.ItemName,
                Description = item.Description,
                ItemType = item.ItemType,
                Location = item.Location,
                Status = item.Status,
                DateReported = item.DateReported,
                Notes = item.Notes,
                ImagePath = item.ImagePath
            };
            return View(vm);
        }

        // POST: LostFound/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LostFoundViewModel vm)
        {
            if (id != vm.ItemID) return NotFound();

            if (vm.ImageUpload != null && !ImageUploadHelper.IsValid(vm.ImageUpload, out var imgErr))
                ModelState.AddModelError(nameof(vm.ImageUpload), imgErr);

            if (!ModelState.IsValid) return View(vm);

            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            item.ItemName = vm.ItemName;
            item.Description = vm.Description;
            item.ItemType = vm.ItemType;
            item.Location = vm.Location;
            item.Status = vm.Status;
            item.Notes = vm.Notes;

            if (vm.ImageUpload != null)
                item.ImagePath = await ImageUploadHelper.SaveAsync(vm.ImageUpload, "lostfound", _env);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Item record updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems.FirstOrDefaultAsync(l => l.ItemID == id);
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

        // POST: LostFound/Resolve/5 — close out an item that's been returned/handed over
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id)
        {
            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            item.Status = "Resolved";
            await _context.SaveChangesAsync();
            TempData["Success"] = "Item marked as resolved.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
