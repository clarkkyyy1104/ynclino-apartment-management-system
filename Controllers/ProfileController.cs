using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoApartmentManagementSystem.Data;

namespace YnclinoApartmentManagementSystem.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Profile  — the tenant's own profile page (info + recent activity)
        public async Task<IActionResult> Index()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int uid))
                return View((YnclinoApartmentManagementSystem.Models.tblTenant?)null);

            var tenant = await _context.tblTenants
                .Include(t => t.Unit)
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
    }
}
