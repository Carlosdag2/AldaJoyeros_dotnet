namespace AldaJoyeros.Helpers;

public static class TrafficPages
{
    // Store section names only: never search terms, product IDs, order numbers or recovery tokens.
    public static string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 512 || !path.StartsWith('/') || path.StartsWith("//") || path.Contains('?') || path.Contains('#')) return null;
        path = path.TrimEnd('/').ToLowerInvariant();
        if (path is "" or "/home" or "/home/index") return "Inicio";
        if (path is "/catalogo" or "/productos" or "/productos/index" || path.StartsWith("/catalogo/")) return "Catálogo";
        if (path.StartsWith("/producto/") || path.StartsWith("/productos/detalle/")) return "Ficha de producto";
        if (path is "/carrito" or "/carrito/index") return "Carrito";
        if (path is "/pedidos/checkout" or "/pedidos/checkoutpago" or "/pedidos/checkoutconfirmacion") return "Proceso de compra";
        if (path is "/aviso-legal" or "/privacidad" or "/cookies" or "/condiciones-de-compra" or "/home/avisolegal" or "/home/privacy" or "/home/cookies" or "/home/terminos") return "Información legal";
        return null;
    }
    public static bool IsAutomated(HttpRequest request)
    {
        var agent = request.Headers.UserAgent.ToString();
        return new[] { "bot", "spider", "crawler", "headless", "lighthouse", "preview" }.Any(word => agent.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
