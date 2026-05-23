using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YnclinoAMS.Controllers
{
    [Authorize]
    public class MaintenanceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
