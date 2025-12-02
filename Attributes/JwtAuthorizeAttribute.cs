using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AldaJoyeros.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class JwtAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string? _requiredRole;

        public JwtAuthorizeAttribute(string? requiredRole = null)
        {
            _requiredRole = requiredRole;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var isAuthenticated = context.HttpContext.Items["IsAuthenticated"] as bool? ?? false;
            var userRole = context.HttpContext.Items["UserRole"] as string;

            if (!isAuthenticated)
            {
                // Usuario no autenticado - redirigir a login con returnUrl
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
                return;
            }

            // Si se requiere un rol específico
            if (!string.IsNullOrEmpty(_requiredRole) && userRole != _requiredRole)
            {
                // Usuario no tiene el rol requerido - retornar 403 Forbidden
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
