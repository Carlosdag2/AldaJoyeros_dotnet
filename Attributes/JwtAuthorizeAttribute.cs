using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AldaJoyeros.Authorization;
using AldaJoyeros.Extensions;

namespace AldaJoyeros.Attributes
{
    /// <summary>
    /// Atributo de autorización JWT personalizado.
    /// Usa ClaimsPrincipal establecido por JwtMiddleware.
    /// </summary>
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
            var user = context.HttpContext.User;

            // Verificar autenticación usando ClaimsPrincipal
            if (!user.IsAuthenticated())
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
                return;
            }

            // Verificar rol si es requerido
            if (!string.IsNullOrEmpty(_requiredRole))
            {
                var userRole = user.GetRole();
                if (userRole != _requiredRole)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Atributo para requerir rol de administrador.
    /// Equivalente a [JwtAuthorize("ADMIN")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminOnlyAttribute : JwtAuthorizeAttribute
    {
        public AdminOnlyAttribute() : base(Roles.Admin) { }
    }

    /// <summary>
    /// Atributo para requerir usuario autenticado (cualquier rol).
    /// Equivalente a [JwtAuthorize()]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthenticatedOnlyAttribute : JwtAuthorizeAttribute
    {
        public AuthenticatedOnlyAttribute() : base(null) { }
    }
}
