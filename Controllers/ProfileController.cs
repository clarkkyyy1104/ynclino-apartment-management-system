using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;
using YnclinoApartmentManagementSystem.Helpers;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Profile  — the tenant's own profile page (info + recent activity)
        public async Task<IActionResult> Index()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
                return View((YnclinoApartmentManagementSystem.Models.tblTenant?)null);

            var tenant = await _context.tblTenants
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserID == uid);

            if (tenant == null)
                return View((YnclinoApartmentManagementSystem.Models.tblTenant?)null);

            // a small activity feed drawn from the tenant's own records
            ViewBag.Bills = await _context.tblBillings
                .Where(b => b.TenantID == tenant.TenantID)
                .OrderByDescending(b => b.BillingPeriod).Take(5).ToListAsync();

            ViewBag.Maintenance = await _context.tblMaintenanceRequests
                .Where(m => m.TenantID == tenant.TenantID)
                .OrderByDescending(m => m.DateSubmitted).Take(5).ToListAsync();

            return View(tenant);
        }

        // POST: Profile/UploadPhoto  — tenant sets/replaces their own profile photo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile? photo)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
                return RedirectToAction(nameof(Index));

            var tenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.UserID == uid);
            if (tenant == null)
            {
                TempData["Error"] = "No tenant profile is linked to your account.";
                return RedirectToAction(nameof(Index));
            }

            if (photo == null)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (!ImageUploadHelper.IsValid(photo, out var err))
            {
                TempData["Error"] = err;
                return RedirectToAction(nameof(Index));
            }

            tenant.PhotoPath = await ImageUploadHelper.SaveAsync(photo, "profiles", _env);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Profile photo updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Profile/RemovePhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
            {
                var tenant = await _context.tblTenants.FirstOrDefaultAsync(t => t.UserID == uid);
                if (tenant != null && tenant.PhotoPath != null)
                {
                    tenant.PhotoPath = null;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Profile photo removed.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
