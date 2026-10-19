using AldaJoyeros.Configuration;
using AldaJoyeros.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace AldaJoyeros.Extensions;

public static class PublicationExtensions
{
    public static IServiceCollection AddPublicationFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PublicationOptions>(configuration.GetSection("Publication"));
        services.AddScoped<PublicImageService>();
        services.AddSingleton<PublicImageCache>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsync("Demasiados intentos. Espera un minuto antes de volver a intentarlo.", ct);
            };
            options.AddPolicy("auth-forms", context => HttpMethods.IsPost(context.Request.Method)
                ? System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                    (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + ":" + (context.GetRouteValue("action")?.ToString() ?? "form").ToLowerInvariant(),
                    _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                    { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 })
                : System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter<string>("read"));
        });

        return services;
    }
    // Run after routing so rate-limit policies and page indexing rules see the selected action.
    public static IApplicationBuilder UsePublicationFeatures(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            var publication = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicationOptions>>().Value;
            var controller = context.GetRouteValue("controller")?.ToString() ?? "";
            var action = context.GetRouteValue("action")?.ToString() ?? "";
            var publicPage = (controller.Equals("Home", StringComparison.OrdinalIgnoreCase) && action is "Index" or "Privacy" or "Terminos" or "Cookies" or "AvisoLegal")
                || (controller.Equals("Productos", StringComparison.OrdinalIgnoreCase) && action is "Index" or "Detalle" && string.IsNullOrWhiteSpace(context.Request.Query["busqueda"]));
            var publicImage = controller is "CatalogoImagen" or "CatalogoPantalla";
            if (!publication.CanIndex || (!publicPage && !publicImage)) context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode >= 400) context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                return Task.CompletedTask;
            });
            await next();
        });
        return app;
    }
    public static IEndpointConventionBuilder MapPublicationFallback(this IEndpointRouteBuilder endpoints)
        => endpoints.MapFallbackToController("NotFoundPage", "Home");
}
