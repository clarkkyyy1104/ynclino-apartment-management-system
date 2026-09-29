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
    [Authorize(Roles = "Admin,Tenant")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Profile  — the signed-in user's own profile page.
        // Admins get an account/overview page; tenants get their profile + activity.
        public async Task<IActionResult> Index()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
                return View((YnclinoApartmentManagementSystem.Models.tblTenant?)null);

            if (User.IsInRole("Admin"))
            {
                var admin = await _context.tblUsers.FirstOrDefaultAsync(u => u.UserID == uid);
                ViewBag.TotalUnits = await _context.tblUnits.CountAsync();
                ViewBag.ActiveTenants = await _context.tblTenants.CountAsync(t => t.Status == "Active");
                ViewBag.PendingTransfers = await _context.tblUnitTransferRequests.CountAsync(r => r.Status == "Pending");
                ViewBag.AdminCount = await _context.tblUsers.CountAsync(u => u.Role == "Admin" && u.IsActive);
                return View("AdminProfile", admin);
            }

            var tenant = await _context.tblTenants
                .Include(t => t.Assignments).ThenInclude(a => a.Unit)
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

            ViewBag.Transfers = await _context.tblUnitTransferRequests
                .Include(r => r.RequestedUnit)
                .Where(r => r.TenantID == tenant.TenantID)
                .OrderByDescending(r => r.DateRequested).Take(5).ToListAsync();

            return View(tenant);
        }

        // POST: Profile/UploadPhoto  — tenant sets/replaces their own profile photo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
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
        [Authorize(Roles = "Tenant")]
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

        // GET: Profile/EditInfo — the tenant's own name, number and emergency contact.
        // This did not exist before: a tenant could see these but not correct them.
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> EditInfo()
        {
            var tenant = await MyTenantAsync();
            if (tenant == null) return RedirectToAction(nameof(Index));

            return View(new ProfileInfoViewModel
            {
                FirstName = tenant.FirstName,
                LastName = tenant.LastName,
                ContactNumber = tenant.ContactNumber,
                EmergencyContactName = tenant.EmergencyContactName,
                EmergencyContactRelationship = tenant.EmergencyContactRelationship,
                EmergencyContactNumber = tenant.EmergencyContactNumber
            });
        }

        // POST: Profile/EditInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> EditInfo(ProfileInfoViewModel vm)
        {
            var tenant = await MyTenantAsync();
            if (tenant == null) return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid) return View(vm);

            static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

            tenant.FirstName = vm.FirstName.Trim();
            tenant.LastName = vm.LastName.Trim();
            tenant.ContactNumber = Clean(vm.ContactNumber);
            tenant.EmergencyContactName = Clean(vm.EmergencyContactName);
            tenant.EmergencyContactRelationship = Clean(vm.EmergencyContactRelationship);
            tenant.EmergencyContactNumber = Clean(vm.EmergencyContactNumber);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your personal information has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // the tenant record behind the signed-in account, or null
        private async Task<tblTenant?> MyTenantAsync() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid)
                ? await _context.tblTenants.FirstOrDefaultAsync(t => t.UserID == uid)
                : null;
    }
}
