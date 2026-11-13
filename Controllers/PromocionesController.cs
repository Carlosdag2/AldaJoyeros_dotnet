using AldaJoyeros.Attributes;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Promotions;
using AldaJoyeros.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PromocionesController(PromotionStore store, PromotionMail mail, CatalogoService catalog) : BaseController
{
    [JwtAuthorize]
    public async Task<IActionResult> Preferencias()
    {
        return View(await store.SubscribedAsync(CurrentUserId!.Value));
    }

    [JwtAuthorize]
    [HttpPost]
    public async Task<IActionResult> Preferencias(bool acepta)
    {
        await store.SubscribeAsync(CurrentUserId!.Value, acepta, "preferencias-web-v1");
        TempData["Success"] = acepta ? "Recibirás nuestras ofertas por correo. Puedes darte de baja cuando quieras." : "No recibirás correos de ofertas.";
        return RedirectToAction(nameof(Preferencias));
    }

    public IActionResult Baja(string? token)
    {
        if (mail.Parse(token) == null)
            return BadRequest("El enlace de baja no es válido.");
        ViewBag.Token = token;
        return View(false);
    }

    [HttpPost]
    public async Task<IActionResult> BajaConfirmar(string? token)
    {
        var data = mail.Parse(token);
        if (data == null)
            return BadRequest("El enlace de baja no es válido.");
        await store.UnsubscribeAsync(data.Value.Id, data.Value.Version);
        return View("Baja", true);
    }

    public async Task<IActionResult> Aviso()
    {
        var state = await store.GetAsync();
        if (!state.PopupEnabled)
            return NoContent();
        var discounted = await catalog.GetAsync(descuentos: "si");
        if (discounted.Count == 0)
            return NoContent();
        var product = state.PopupProductId.HasValue ? discounted.SingleOrDefault(p => p.Id == state.PopupProductId) : null;
        if (state.PopupProductId.HasValue && product == null)
            return NoContent();
        if (product?.TieneTallas == true)
        {
            var size = product.Tallas.Where(t => t.DescuentoPorcentaje > 0 && t.PrecioSinDescuento > t.Precio).OrderBy(t => t.Precio).FirstOrDefault();
            if (size != null)
                product = await catalog.DetailAsync(product.Id, size.ProductoId);
        }

        if (product != null)
            await catalog.LoadImagesAsync(new[] { product });
        return Json(new { version = state.PopupVersion, seconds = state.PopupSeconds, name = product == null ? null : product.Nombre + (PublicUrls.SelectedSize(product)is { } chosen ? " · Talla " + chosen : ""), title = product == null ? "Una selección con descuento" : "Una pieza especial para ti", price = product?.Precio.ToString("C"), previous = product?.PrecioSinDescuento?.ToString("C"), percent = product?.DescuentoPorcentaje.ToString("0.##"), reference = product?.PrecioAnterior != product?.PrecioSinDescuento ? product?.PrecioAnterior?.ToString("C") : null, image = product?.ImagenPrincipal, url = product == null ? (Request.PathBase + "/catalogo?descuentos=si").ToString() : PublicUrls.ProductLink(Request, product) });
    }
}
