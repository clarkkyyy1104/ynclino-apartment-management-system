using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YnclinoAMS.Controllers
{
    [Authorize]
    public class LostFoundController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
