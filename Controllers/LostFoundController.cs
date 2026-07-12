using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Models;
using YnclinoApartmentManagementSystem.Models.ViewModels;

namespace YnclinoApartmentManagementSystem.Controllers
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

            // tenants see their own reports plus anything marked Found
            if (User.IsInRole("Tenant"))
            {
                var uid = CurrentUserID();
                query = query.Where(l => l.ReportedByUserID == uid || l.ItemType == "Found");
            }

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
            ViewBag.CurrentUserID = CurrentUserID();

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

            ViewBag.CurrentUserID = CurrentUserID();

            if (User.IsInRole("Tenant") && item.ItemType != "Found" && item.ReportedByUserID != CurrentUserID())
                return Forbid();

            if (User.IsInRole("Admin") || User.IsInRole("SemiAdmin"))
            {
                ViewBag.Claims = await _context.tblClaimRequests
                    .Include(c => c.Claimant)
                    .Where(c => c.ItemID == id)
                    .OrderByDescending(c => c.SubmittedAt)
                    .ToListAsync();
            }
            else if (User.IsInRole("Tenant"))
            {
                // a tenant sees only the claims they filed on this item
                var uid = CurrentUserID();
                ViewBag.MyClaims = await _context.tblClaimRequests
                    .Where(c => c.ItemID == id && c.ClaimantUserID == uid)
                    .OrderByDescending(c => c.SubmittedAt)
                    .ToListAsync();
            }

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
                ItemName = vm.ItemName,
                Description = vm.Description,
                ItemType = vm.ItemType,
                Location = vm.Location,
                Status = "Reported",
                DateReported = DateTime.Now,
                Notes = vm.Notes
            };

            _context.tblLostFoundItems.Add(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{vm.ItemType} item \"{vm.ItemName}\" has been reported.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Edit/5
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.tblLostFoundItems
                .Include(l => l.ReportedBy)
                .FirstOrDefaultAsync(l => l.ItemID == id);

            if (item == null) return NotFound();

            var vm = new LostFoundViewModel
            {
                ItemID = item.ItemID,
                ReportedByUserID = item.ReportedByUserID,
                ReportedByName = item.ReportedBy?.Username,
                ItemName = item.ItemName,
                Description = item.Description,
                ItemType = item.ItemType,
                Location = item.Location,
                Status = item.Status,
                DateReported = item.DateReported,
                Notes = item.Notes
            };
            return View(vm);
        }

        // POST: LostFound/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> Edit(int id, LostFoundViewModel vm)
        {
            if (id != vm.ItemID) return NotFound();
            if (!ModelState.IsValid)
            {
                vm.ReportedByName = (await _context.tblUsers.FindAsync(vm.ReportedByUserID))?.Username;
                return View(vm);
            }

            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            item.ItemName = vm.ItemName;
            item.Description = vm.Description;
            item.ItemType = vm.ItemType;
            item.Location = vm.Location;
            item.Status = vm.Status;
            item.Notes = vm.Notes;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Item record updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Delete/5
        [Authorize(Roles = "Admin,SemiAdmin")]
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
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null) return NotFound();

            _context.tblLostFoundItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Item record deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LostFound/Claim/5
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Claim(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null || item.ItemType != "Found" || item.Status != "Reported")
                return NotFound();

            var uid = CurrentUserID();

            // you can't claim an item you reported yourself
            if (item.ReportedByUserID == uid)
            {
                TempData["Error"] = "You reported this item, so you cannot file a claim on it.";
                return RedirectToAction(nameof(Index));
            }

            // don't let the same person stack pending claims on one item
            bool alreadyClaimed = await _context.tblClaimRequests
                .AnyAsync(c => c.ItemID == id && c.ClaimantUserID == uid && c.Status == "Pending");
            if (alreadyClaimed)
            {
                TempData["Error"] = "You already have a pending claim for this item.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Item = item;
            return View();
        }

        // POST: LostFound/Claim/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Claim(int id, string verificationDetails)
        {
            var item = await _context.tblLostFoundItems.FindAsync(id);
            if (item == null || item.ItemType != "Found" || item.Status != "Reported")
                return NotFound();

            var uid = CurrentUserID();
            if (uid == null) return Forbid();

            if (item.ReportedByUserID == uid)
            {
                TempData["Error"] = "You reported this item, so you cannot file a claim on it.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(verificationDetails) || verificationDetails.Length > 1000)
            {
                TempData["Error"] = "Please describe your proof of ownership (up to 1000 characters).";
                return RedirectToAction(nameof(Claim), new { id });
            }

            // re-check for a pending duplicate in case of a double submit
            bool alreadyClaimed = await _context.tblClaimRequests
                .AnyAsync(c => c.ItemID == id && c.ClaimantUserID == uid && c.Status == "Pending");
            if (alreadyClaimed)
            {
                TempData["Error"] = "You already have a pending claim for this item.";
                return RedirectToAction(nameof(Index));
            }

            var claim = new tblClaimRequest
            {
                ItemID = id,
                ClaimantUserID = uid.Value,
                VerificationDetails = verificationDetails,
                SubmittedAt = DateTime.Now,
                Status = "Pending"
            };
            _context.tblClaimRequests.Add(claim);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Your claim has been submitted. An admin will review it.";
            return RedirectToAction(nameof(Index));
        }

        // POST: LostFound/ReviewClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
        public async Task<IActionResult> ReviewClaim(int claimId, string decision, string? adminNotes)
        {
            if (decision != "Approved" && decision != "Rejected")
                return BadRequest();

            var claim = await _context.tblClaimRequests
                .Include(c => c.Item)
                .FirstOrDefaultAsync(c => c.ClaimID == claimId);
            if (claim == null) return NotFound();

            if (claim.Status != "Pending")
            {
                TempData["Error"] = "This claim has already been reviewed.";
                return RedirectToAction(nameof(Details), new { id = claim.ItemID });
            }

            claim.Status = decision;
            claim.AdminNotes = adminNotes;

            if (decision == "Approved" && claim.Item != null)
            {
                claim.Item.Status = "Claimed";

                // any other pending claims on the same item lose automatically
                var others = await _context.tblClaimRequests
                    .Where(c => c.ItemID == claim.ItemID && c.ClaimID != claim.ClaimID && c.Status == "Pending")
                    .ToListAsync();
                foreach (var other in others)
                {
                    other.Status = "Rejected";
                    other.AdminNotes = "Another claim was approved for this item.";
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Claim has been {decision.ToLower()}.";
            return RedirectToAction(nameof(Details), new { id = claim.ItemID });
        }

        // POST: LostFound/Resolve/5 — close out an item that's been handed over
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SemiAdmin")]
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
