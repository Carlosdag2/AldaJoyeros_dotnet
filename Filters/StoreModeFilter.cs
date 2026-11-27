using AldaJoyeros.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AldaJoyeros.Filters;

public sealed class StoreModeFilter(StoreModeStore store, Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.StorageOptimizationOptions>? optimization = null) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller);
        context.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
        // A product photo is served in both modes and never renders the store navigation.
        // Its own controller still checks product visibility against MySQL on every request.
        if ((optimization?.Value.Enabled ?? true) && controller == "CatalogoImagen" && action == "Foto")
        {
            await next();
            return;
        }
        var enabled = await store.EnabledAsync();
        context.HttpContext.Items[StoreModeStore.ContextKey] = enabled;
        var orderHistory = action is "Index" or "Detalle" or "PedidoCompletado";
        var purchase = string.Equals(controller, "Carrito", StringComparison.OrdinalIgnoreCase)
            || string.Equals(controller, "Pedidos", StringComparison.OrdinalIgnoreCase) && !orderHistory;
        if (!enabled && purchase)
        {
            var ajax = context.HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest"
                || action?.EndsWith("Ajax", StringComparison.OrdinalIgnoreCase) == true;
            if (HttpMethods.IsGet(context.HttpContext.Request.Method) && !ajax)
                context.Result = new RedirectToActionResult("Index", "Productos", null);
            else
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "La web está en modo escaparate. Contacta con la tienda para consultar este producto."
                }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }
        await next();
    }
}
