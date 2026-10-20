using AldaJoyeros.Configuration;
using AldaJoyeros.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Filters;

// Only public HTML navigation is permanent. Forms, APIs and authentication retain their own semantics.
public sealed class PublicUrlFilter(IOptions<PublicationOptions> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if ((!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            || request.Headers.XRequestedWith == "XMLHttpRequest") { await next(); return; }
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action = context.RouteData.Values["action"]?.ToString() ?? "";
        string? path = null;
        if (controller.Equals("Home", StringComparison.OrdinalIgnoreCase)) PublicUrls.HomePaths.TryGetValue(action, out path);
        if (controller.Equals("Productos", StringComparison.OrdinalIgnoreCase) && action.Equals("Index", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(context.RouteData.Values["categoria"]?.ToString())) path = "/catalogo";
        if (path != null && (!string.Equals(request.Path.Value, path, StringComparison.Ordinal) || PublicUrls.ShouldRedirectOrigin(request, options.Value)))
        {
            context.Result = new RedirectResult(PublicUrls.Destination(request, options.Value, path, request.QueryString.Value ?? ""), permanent: true);
            return;
        }
        await next();
    }
}
