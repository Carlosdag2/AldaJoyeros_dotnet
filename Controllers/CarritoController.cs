using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers;

[ServiceFilter(typeof(AldaJoyeros.Filters.CartSessionFilter))]
[AutoValidateAntiforgeryToken]
public class CarritoController(GuestCartService cart, IProductoImagenService images) : BaseController
{
    public async Task<IActionResult> Index()
    {
        var items = await cart.GetAsync(CurrentUserId);
        foreach (var item in items)
            if (item.Producto != null && !item.Producto.TieneImagenes)
                item.Producto.Imagenes = (await images.GetByProductoIdAsync(item.ProductoId)).ToList();
        ViewBag.Total = items.Sum(i => i.Subtotal);
        ViewBag.PendingCart = CurrentUserId.HasValue && cart.PendingMerge;
        if (cart.Notices.Count > 0) TempData["Warning"] = string.Join(" ", cart.Notices.Distinct());
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Agregar(long productoId, int cantidad = 1)
    {
        try { var name = await cart.AddAsync(CurrentUserId, productoId, cantidad); TempData["Success"] = $"'{name}' añadido al carrito"; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> AgregarAjax(long productoId, int cantidad = 1)
    {
        try { var name = await cart.AddAsync(CurrentUserId, productoId, cantidad); return await SummaryAsync($"'{name}' añadido al carrito"); }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }
    [HttpPost]
    public async Task<IActionResult> Actualizar(long itemId, int cantidad) {
        try { await cart.UpdateAsync(CurrentUserId, itemId, cantidad); TempData["Success"] = "Cantidad actualizada"; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Eliminar(long itemId) {
        try { await cart.RemoveAsync(CurrentUserId, itemId); TempData["Success"] = "Producto eliminado del carrito"; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Vaciar() {
        try { await cart.ClearAsync(CurrentUserId); TempData["Success"] = "Carrito vaciado"; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> ActualizarAjax(long itemId, int cantidad) {
        try { await cart.UpdateAsync(CurrentUserId, itemId, cantidad); return await SummaryAsync("Cantidad actualizada", itemId); }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }
    [HttpPost]
    public async Task<IActionResult> EliminarAjax(long itemId) {
        try { await cart.RemoveAsync(CurrentUserId, itemId); return await SummaryAsync("Producto eliminado del carrito"); }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }
    [HttpPost]
    public async Task<IActionResult> VaciarAjax() {
        try { await cart.ClearAsync(CurrentUserId); return await SummaryAsync("Carrito vaciado"); }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }
    [HttpGet]
    public Task<IActionResult> GetResumenAjax() => SummaryAsync("");

    private async Task<IActionResult> SummaryAsync(string message, long? itemId = null) {
        var items = await cart.GetAsync(CurrentUserId);
        var total = items.Sum(i => i.Subtotal);
        var subtotal = items.FirstOrDefault(i => i.Id == itemId)?.Subtotal ?? 0;
        return Json(new { success = true, message, total, totalFormateado = total.ToString("C"),
            subtotal, subtotalFormateado = subtotal.ToString("C"), totalItems = items.Sum(i => i.Cantidad),
            carritoVacio = items.Count == 0, items = items.Select(i => new { id = i.Id, productoId = i.ProductoId,
                nombre = i.ProductoNombre, precio = i.Precio, cantidad = i.Cantidad, subtotal = i.Subtotal }) });
    }
}
