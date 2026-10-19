using AldaJoyeros.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;

public sealed class ConsentController : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(bool analytics, string? returnUrl)
    {
        var path = Request.PathBase.HasValue ? Request.PathBase.Value! : "/";
        Response.Cookies.Append(VisitorConsent.CookieName, analytics ? "v1.analytics" : "v1.necessary", new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            IsEssential = true, Path = path, MaxAge = TimeSpan.FromDays(180)
        });
        if (!analytics) Response.Cookies.Delete(VisitorConsent.VisitorCookie, new CookieOptions { Path = path });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
    }
}
