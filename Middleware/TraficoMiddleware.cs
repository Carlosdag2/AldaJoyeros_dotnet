using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Middleware
{
    public class TraficoMiddleware
    {
        private readonly RequestDelegate _next;

        public TraficoMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITraficoService traficoService)
        {
            // Solo rastrear peticiones de páginas, no recursos estáticos ni AJAX
            var path = context.Request.Path.Value?.ToLower() ?? "";
            
            if (AldaJoyeros.Helpers.VisitorConsent.AllowsAnalytics(context.Request) && HttpMethods.IsGet(context.Request.Method) && DebeRastrear(path, context))
            {
                var sessionId = ObtenerOCrearSessionId(context);
                traficoService.RegistrarVisita(sessionId, path);
            }

            if (!AldaJoyeros.Helpers.VisitorConsent.HasChoice(context.Request) && context.Request.Cookies.ContainsKey(".AldaJoyeros.Visitor"))
                context.Response.Cookies.Delete(".AldaJoyeros.Visitor", new CookieOptions { Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/" });
            await _next(context);
        }

        private bool DebeRastrear(string path, HttpContext context)
        {
            // Ignorar recursos estáticos
            if (path.StartsWith("/catalogoimagen/") || path.StartsWith("/catalogopantalla/") || path.StartsWith("/auth/") || path.StartsWith("/admin") || path.StartsWith("/lib/") ||
                path.StartsWith("/css/") || 
                path.StartsWith("/js/") || 
                path.StartsWith("/images/") ||
                path.StartsWith("/_") ||
                path.EndsWith(".css") ||
                path.EndsWith(".js") ||
                path.EndsWith(".map") ||
                path.EndsWith(".ico") ||
                path.EndsWith(".png") ||
                path.EndsWith(".jpg") ||
                path.EndsWith(".svg") ||
                path.EndsWith(".woff") ||
                path.EndsWith(".woff2"))
            {
                return false;
            }

            // Ignorar peticiones AJAX
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return false;
            }

            // Ignorar endpoints de API
            if (path.Contains("/api/") || path.Contains("ajax"))
            {
                return false;
            }

            return true;
        }

        private string ObtenerOCrearSessionId(HttpContext context)
        {
            const string cookieName = ".AldaJoyeros.Visitor";
            
            if (context.Request.Cookies.TryGetValue(cookieName, out var sessionId) && !string.IsNullOrEmpty(sessionId))
            {
                return sessionId;
            }

            sessionId = Guid.NewGuid().ToString("N");
            
            context.Response.Cookies.Append(cookieName, sessionId, new CookieOptions
            {
                Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/",
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(1)
            });

            return sessionId;
        }
    }

    public static class TraficoMiddlewareExtensions
    {
        public static IApplicationBuilder UseTrafico(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TraficoMiddleware>();
        }
    }
}
