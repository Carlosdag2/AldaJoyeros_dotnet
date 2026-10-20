using AldaJoyeros.Helpers;

namespace AldaJoyeros.Extensions;

public static class PublicRouteExtensions
{
    // Register before the legacy conventional route so URL generation prefers the public paths.
    public static void MapPublicRoutes(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute("public-product", "producto/{slug:regex(.*[a-zA-Z-].*)}", new { controller = "Productos", action = "Detalle" });
        endpoints.MapControllerRoute("legacy-product-id", "producto/{id:long}/{oldSlug?}", new { controller = "Productos", action = "Detalle" });
        endpoints.MapControllerRoute("public-catalog", "catalogo/{categoria?}", new { controller = "Productos", action = "Index" });
        foreach (var entry in PublicUrls.HomePaths)
            endpoints.MapControllerRoute("public-home-" + entry.Key, entry.Value.TrimStart('/'), new { controller = "Home", action = entry.Key });
    }
}
