using AldaJoyeros.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;

public sealed class ConsentController(AldaJoyeros.Services.Interfaces.ITraficoService traffic) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(bool analytics, string? returnUrl, bool maps = false, string? choice = null)
    {
        if (choice is not (null or "necessary" or "all" or "selected")) return BadRequest();
        if (choice == "all") { analytics = true; maps = true; }
        else if (choice == "necessary") { analytics = false; maps = false; }
        else if (choice == null) maps = VisitorConsent.AllowsMaps(Request); // Older analytics-only forms.
        return SavePreferences(analytics, maps, returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Maps(bool maps, string? returnUrl) => SavePreferences(VisitorConsent.AllowsAnalytics(Request), maps, returnUrl);

    private IActionResult SavePreferences(bool analytics, bool maps, string? returnUrl)
    {
        Response.Headers.CacheControl = "no-store";
        var ajax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        var previouslyAllowed = VisitorConsent.AllowsAnalytics(Request);
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/");
        var logicalPage = Url.Content(destination).Split(new[] { '?', '#' }, 2)[0];
        if (Request.PathBase.HasValue && logicalPage.Equals(Request.PathBase.Value, StringComparison.OrdinalIgnoreCase)) logicalPage = "/";
        else if (Request.PathBase.HasValue && logicalPage.StartsWith(Request.PathBase.Value + "/", StringComparison.OrdinalIgnoreCase)) logicalPage = logicalPage[Request.PathBase.Value.Length..];
        var trackable = TrafficPages.Resolve(logicalPage) != null && !TrafficPages.IsAutomated(Request);
        var path = Request.PathBase.HasValue ? Request.PathBase.Value! : "/";
        Response.Cookies.Append(VisitorConsent.CookieName, VisitorConsent.Encode(analytics, maps), new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            IsEssential = true, Path = path, MaxAge = TimeSpan.FromDays(180)
        });
        if (!analytics) {
            if (Request.Cookies[VisitorConsent.VisitorCookie] is { } visitor) traffic.RetirarConsentimiento(visitor);
            Response.Cookies.Delete(VisitorConsent.VisitorCookie, new CookieOptions { Path = path });
        }
        if (ajax)
        {
            if (analytics && trackable)
            {
                var visitor = Request.Cookies[VisitorConsent.VisitorCookie];
                if (!previouslyAllowed || !Guid.TryParseExact(visitor, "N", out _))
                {
                    visitor = Guid.NewGuid().ToString("N");
                    Response.Cookies.Append(VisitorConsent.VisitorCookie, visitor, new CookieOptions {
                        Path=path,HttpOnly=true,Secure=Request.IsHttps,SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromDays(1)
                    });
                    traffic.RegistrarVisita(visitor, logicalPage);
                }
            }
            return Json(new { success=true, analytics, maps, description=VisitorConsent.Description(analytics, maps), trafficEnabled=analytics && trackable,
                message="Preferencias guardadas: " + VisitorConsent.Description(analytics, maps) + "." });
        }
        return LocalRedirect(destination);
    }
}
