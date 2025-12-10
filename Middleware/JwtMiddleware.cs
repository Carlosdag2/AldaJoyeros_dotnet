using System.Security.Claims;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IJwtService jwtService)
        {
            var token = context.Request.Cookies["jwt_token"];

            if (!string.IsNullOrEmpty(token))
            {
                var usuario = jwtService.GetUserFromToken(token);
                
                if (usuario != null)
                {
                    // Crear ClaimsPrincipal para integración nativa con [Authorize]
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                        new(ClaimTypes.Email, usuario.Email),
                        new(ClaimTypes.Role, usuario.Rol ?? "USER")
                    };

                    var identity = new ClaimsIdentity(claims, "jwt");
                    context.User = new ClaimsPrincipal(identity);

                    // Mantener HttpContext.Items para compatibilidad y acceso rápido
                    context.Items["CurrentUser"] = usuario;
                    context.Items["IsAuthenticated"] = true;
                }
            }

            await _next(context);
        }
    }
}
