using AldaJoyeros.Attributes;
using AldaJoyeros.Services;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;

[JwtAuthorize("ADMIN")]
public sealed class AdminTiendaController(StoreModeStore store) : BaseController
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cambiar(bool ecommerce)
    {
        await store.SetAsync(ecommerce);
        TempData["Success"] = ecommerce ? "Tienda online activada. Ya están disponibles el carrito y la compra." : "Modo escaparate activado. El catálogo sigue visible y las compras están deshabilitadas.";
        return RedirectToAction("Index", "Admin");
    }
}
