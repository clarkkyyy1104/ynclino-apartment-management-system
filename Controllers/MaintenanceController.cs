using Microsoft.AspNetCore.Mvc;

namespace YnclinoAMS.Controllers
{
    public class MaintenanceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
