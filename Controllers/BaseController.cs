using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AldaJoyeros.DTOs;
using AldaJoyeros.Extensions;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class BaseController : Controller
    {
        // Acceso a información del usuario usando ClaimsPrincipal (estándar ASP.NET Core)
        protected long? CurrentUserId => User.GetUserId();
        protected string? CurrentUserEmail => User.GetEmail();
        protected string? CurrentUserRole => User.GetRole();
        protected bool IsAuthenticated => User.IsAuthenticated();
        protected bool IsAdmin => User.IsAdmin();

        // Para compatibilidad con código existente que usa el DTO completo
        protected UsuarioDto? CurrentUser => HttpContext.Items["CurrentUser"] as UsuarioDto;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Pasar información a las vistas
            ViewBag.CurrentUser = CurrentUser;
            ViewBag.IsAuthenticated = IsAuthenticated;
            ViewBag.IsAdmin = IsAdmin;

            // Obtener contador del carrito de forma asíncrona
            ViewBag.CarritoCount = await GetCarritoCountAsync();

            await next();
        }

        private async Task<int> GetCarritoCountAsync()
        {
            if (!IsAuthenticated || !CurrentUserId.HasValue)
                return 0;

            var carritoService = HttpContext.RequestServices.GetService<ICarritoService>();
            if (carritoService == null)
                return 0;

            try
            {
                return await carritoService.GetTotalItemsAsync(CurrentUserId.Value);
            }
            catch
            {
                return 0;
            }
        }
    }
}
