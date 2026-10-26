using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Middleware;

public sealed class TraficoMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITraficoService traffic)
    {
        var request = context.Request;
        if (!VisitorConsent.AllowsAnalytics(request) && request.Cookies.TryGetValue(VisitorConsent.VisitorCookie, out var withdrawn)) {
            traffic.RetirarConsentimiento(withdrawn);
            context.Response.Cookies.Delete(VisitorConsent.VisitorCookie, new CookieOptions { Path = request.PathBase.HasValue ? request.PathBase.Value : "/" });
        }
        if (VisitorConsent.AllowsAnalytics(request) && HttpMethods.IsGet(request.Method)
            && request.Headers.XRequestedWith != "XMLHttpRequest" && TrafficPages.Resolve(request.Path) != null && !TrafficPages.IsAutomated(request)) {
            var visitor = request.Cookies[VisitorConsent.VisitorCookie];
            var existing = Guid.TryParseExact(visitor, "N", out _);
            visitor = existing ? visitor : Guid.NewGuid().ToString("N");
            context.Response.OnStarting(() => {
                // Do not count missing pages, redirects, failed requests, JSON APIs or image downloads.
                if (context.Response.StatusCode == 200 && context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true) {
                    if (!existing) context.Response.Cookies.Append(VisitorConsent.VisitorCookie, visitor!, new CookieOptions {
                        Path=request.PathBase.HasValue?request.PathBase.Value:"/",HttpOnly=true,Secure=request.IsHttps,
                        SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromDays(1)
                    });
                    traffic.RegistrarVisita(visitor!, request.Path);
                }
                return Task.CompletedTask;
            });
        }
        await next(context);
    }
}
public static class TraficoMiddlewareExtensions
{
    public static IApplicationBuilder UseTrafico(this IApplicationBuilder builder) => builder.UseMiddleware<TraficoMiddleware>();
}
