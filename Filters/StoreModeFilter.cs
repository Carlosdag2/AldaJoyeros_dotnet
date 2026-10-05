using AldaJoyeros.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AldaJoyeros.Filters;

public sealed class StoreModeFilter(StoreModeStore store) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var enabled = await store.EnabledAsync();
        context.HttpContext.Items[StoreModeStore.ContextKey] = enabled;
        // Los nombres resueltos por MVC no dependen de la capitalización de la URL.
        context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller);
        context.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
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
