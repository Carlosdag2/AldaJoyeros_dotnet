using AldaJoyeros.Attributes;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Models;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;

[JwtAuthorize("ADMIN")]
public sealed class AdminDestacadosController(IProductoService products, FeaturedProductsStore featured) : BaseController
{
    public async Task<IActionResult> Index(string busqueda = "", int page = 1)
    {
        var all = (await products.GetAllIncludingDeletedAsync(cargarImagenes: false)).ToList();
        var ids = await featured.GetIdsAsync();
        var lookup = all.ToDictionary(p => p.Id);
        var selected = ids.Select(id => (Id: id, Product: lookup.GetValueOrDefault(id))).ToList();
        busqueda = (busqueda ?? "").Trim();
        var candidates = all.Where(p => !p.Eliminado && (busqueda.Length == 0 ||
            p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
            p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) || p.Id.ToString() == busqueda));
        var paged = PagedResult<ProductoDto>.Create(candidates, page, 12);
        await products.CargarImagenesAsync(paged.Items.Concat(selected.Where(s => s.Product != null).Select(s => s.Product!)).DistinctBy(p => p.Id));
        return View(new FeaturedProductsViewModel { Selected = selected, Candidates = paged, Search = busqueda });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cambiar(long id, string accion, string busqueda = "", int page = 1)
    {
        try
        {
            await featured.ChangeAsync(id, accion);
            TempData["Success"] = "Selección de destacados actualizada. El cambio ya se muestra en el inicio.";
        }
        catch (ArgumentException error) { TempData["Warning"] = error.Message; }
        return RedirectToAction(nameof(Index), new { busqueda, page });
    }
}
