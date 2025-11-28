using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AldaJoyeros.Helpers;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class BaseController : Controller
    {
        protected UsuarioDto? CurrentUser => HttpContext.Session.GetObject<UsuarioDto>("CurrentUser");
        protected bool IsAuthenticated => CurrentUser != null;
        protected bool IsAdmin => CurrentUser?.Rol == "ADMIN";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
        
            ViewBag.CurrentUser = CurrentUser;
            ViewBag.IsAuthenticated = IsAuthenticated;
            ViewBag.IsAdmin = IsAdmin;

            // Obtener el contador del carrito si el usuario está autenticado
            if (IsAuthenticated)
            {
                var carritoService = HttpContext.RequestServices.GetService<ICarritoService>();
                if (carritoService != null)
                {
                    var itemsTask = carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                    itemsTask.Wait();
                    var items = itemsTask.Result;
                    ViewBag.CarritoCount = items.Sum(i => i.Cantidad);
                }
                else
                {
                    ViewBag.CarritoCount = 0;
                }
            }
            else
            {
                ViewBag.CarritoCount = 0;
            }
        }
    }
}
