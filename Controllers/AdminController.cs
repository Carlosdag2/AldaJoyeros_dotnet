using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers
{
    public class AdminController : BaseController
    {
        public IActionResult Index()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }
    }
}
