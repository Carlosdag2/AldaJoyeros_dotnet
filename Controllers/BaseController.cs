using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class BaseController : Controller
    {
        protected UsuarioDto? CurrentUser
        {
            get => HttpContext.Session.GetObject<UsuarioDto>("CurrentUser");
            set => HttpContext.Session.SetObject("CurrentUser", value);
        }

        protected bool IsAuthenticated => CurrentUser != null;

        protected bool IsAdmin => CurrentUser?.Rol == "ADMIN";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ViewBag.IsAuthenticated = IsAuthenticated;
            ViewBag.IsAdmin = IsAdmin;
            ViewBag.CurrentUser = CurrentUser;
            base.OnActionExecuting(context);
        }
    }
}
