using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YnclinoAMS.Data;

namespace YnclinoAMS.Controllers
{
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tenants = await _context.tblTenants
                .Include(t => t.Unit)
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.LastName)
                .ToListAsync();
            return View(tenants);
        }
    }
}
