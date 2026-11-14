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
        var content=await PromotionPopupContent.CreateAsync(state,catalog,Request);
        return content==null?NoContent():Json(content);
    }
}
