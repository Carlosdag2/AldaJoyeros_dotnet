namespace AldaJoyeros.Helpers;

public static class VisitorConsent
{
    public const string CookieName = ".AldaJoyeros.Consent";
    public const string VisitorCookie = ".AldaJoyeros.Visitor";
    public static bool HasChoice(HttpRequest request) => request.Cookies[CookieName] is "v1.necessary" or "v1.analytics";
    public static bool AllowsAnalytics(HttpRequest request) => request.Cookies[CookieName] == "v1.analytics";
}
