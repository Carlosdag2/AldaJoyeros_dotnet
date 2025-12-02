using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AldaJoyeros.Helpers;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class BaseController : Controller
    {
        // Obtener usuario desde JWT middleware (sin consultar BD)
        protected UsuarioDto? CurrentUser => HttpContext.Items["CurrentUser"] as UsuarioDto;
            
        protected bool IsAuthenticated => HttpContext.Items["IsAuthenticated"] as bool? ?? false;
        protected bool IsAdmin => CurrentUser?.Rol == "ADMIN";
        
        // Exponer el UserId directamente desde el contexto JWT
        protected long? CurrentUserId => HttpContext.Items["UserId"] as long?;
        protected string? CurrentUserEmail => HttpContext.Items["UserEmail"] as string;
        protected string? CurrentUserRole => HttpContext.Items["UserRole"] as string;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
        
            // Pasar información del JWT a las vistas
            ViewBag.CurrentUser = CurrentUser;
            ViewBag.IsAuthenticated = IsAuthenticated;
            ViewBag.IsAdmin = IsAdmin;

            // Obtener el contador del carrito si el usuario está autenticado
            if (IsAuthenticated && CurrentUserId.HasValue)
            {
                var carritoService = HttpContext.RequestServices.GetService<ICarritoService>();
                if (carritoService != null)
                {
                    try
                    {
                        var itemsTask = carritoService.GetByUsuarioIdAsync(CurrentUserId.Value);
                        itemsTask.Wait();
                        var items = itemsTask.Result;
                        ViewBag.CarritoCount = items.Sum(i => i.Cantidad);
                    }
                    catch
                    {
                        ViewBag.CarritoCount = 0;
                    }
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
