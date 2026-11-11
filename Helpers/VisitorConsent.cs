namespace AldaJoyeros.Helpers;

public static class VisitorConsent
{
    public const string CookieName = ".AldaJoyeros.Consent";
    public const string VisitorCookie = ".AldaJoyeros.Visitor";
    // Earlier consent covered analytics only; it never grants the new Google Maps purpose.
    public static bool HasChoice(HttpRequest request) => request.Cookies[CookieName] is "v1.necessary" or "v1.analytics" or "v2.necessary" or "v2.analytics" or "v2.maps" or "v2.all";
    public static bool AllowsAnalytics(HttpRequest request) => request.Cookies[CookieName] is "v1.analytics" or "v2.analytics" or "v2.all";
    public static bool AllowsMaps(HttpRequest request) => request.Cookies[CookieName] is "v2.maps" or "v2.all";
    public static string Encode(bool analytics, bool maps) => (analytics, maps) switch {
        (true, true) => "v2.all", (true, false) => "v2.analytics", (false, true) => "v2.maps", _ => "v2.necessary"
    };
    public static string Description(bool analytics, bool maps) => (analytics, maps) switch {
        (true, true) => "Analítica y mapa permitidos", (true, false) => "Analítica permitida; mapa bloqueado",
        (false, true) => "Mapa permitido; analítica desactivada", _ => "Solo cookies necesarias"
    };
}
