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
            // Intentar obtener token de la cookie
            var token = context.Request.Cookies["jwt_token"];

            if (!string.IsNullOrEmpty(token))
            {
                // Extraer toda la información del usuario directamente del token
                var usuario = jwtService.GetUserFromToken(token);
                
                if (usuario != null)
                {
                    // Agregar información del usuario al contexto (SIN consultar BD)
                    context.Items["UserId"] = usuario.Id;
                    context.Items["UserEmail"] = usuario.Email;
                    context.Items["UserRole"] = usuario.Rol;
                    context.Items["CurrentUser"] = usuario;
                    context.Items["IsAuthenticated"] = true;
                }
            }

            await _next(context);
        }
    }
}
