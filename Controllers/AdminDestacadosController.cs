using AldaJoyeros.Attributes;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Models;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace AldaJoyeros.Controllers;

[JwtAuthorize("ADMIN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDestacadosController(IProductoService products, ICategoriaService categories,
    CatalogoService catalog, FeaturedProductsStore featured, ICompositeViewEngine views) : BaseController
{
    private bool Ajax => Request.Headers.XRequestedWith == "XMLHttpRequest";
    private async Task<List<(long Id, ProductoDto? Product)>> SelectionAsync()
    {
        var ids = await featured.GetIdsAsync();
        var parents = (await catalog.FamiliesAsync()).SelectMany(f => f.Members.Select(m => (m.Id, f.Product))).ToDictionary(p => p.Id, p => p.Product);
        var selected = new List<(long Id, ProductoDto? Product)>();
        foreach (var id in ids)
            selected.Add((id, parents.GetValueOrDefault(id) ?? await products.GetByIdAsync(id)));
        await catalog.LoadImagesAsync(selected.Where(s => s.Product != null).Select(s => s.Product!));
        return selected;
    }
    private async Task<FeaturedProductsViewModel> ModelAsync(string? search, int page, long? categoryId)
    {
        var rows = (await categories.GetAllAsync()).ToList();
        if (categoryId.HasValue && !rows.Any(c => c.Id == categoryId)) throw new ArgumentException("La categoría seleccionada ya no está disponible.");
        var all = await catalog.GetAsync();
        foreach (var row in rows) row.CantidadProductos = all.Count(p => p.CategoriaId == row.Id);
        foreach (var parent in rows.Where(c => !c.Nombre.Contains(" · ")))
            parent.CantidadProductos += rows.Where(c => c.Nombre.StartsWith(parent.Nombre + " · ", StringComparison.Ordinal)).Sum(c => c.CantidadProductos);
        search = (search ?? "").Trim();
        var candidates = (await catalog.GetAsync(categoryId)).Where(p => search.Length == 0 || p.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));
        var paged = PagedResult<ProductoDto>.Create(candidates, page, 12);
        await catalog.LoadImagesAsync(paged.Items);
        return new FeaturedProductsViewModel { Selected = await SelectionAsync(), Candidates = paged, Search = search, CategoryId = categoryId,
            Categories = rows.Where(c => c.CantidadProductos > 0 && c.Grupo != "Pendientes de organizar").OrderBy(c => AldaJoyeros.Catalog.CatalogTaxonomy.GroupOrder(c.Grupo)).ThenBy(c => c.Nombre).ToList() };
    }
    [HttpGet]
    public async Task<IActionResult> Index(string busqueda = "", int page = 1, long? categoriaId = null)
    {
        try {
            var model = await ModelAsync(busqueda, page, categoriaId);
            if (!Ajax) return View(model);
            return Json(new { success = true, selectionHtml = await RenderAsync("_SeleccionDestacados", model), candidatesHtml = await RenderAsync("_CandidatosDestacados", model), page = model.Candidates.PageNumber });
        } catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
    [HttpGet]
    public async Task<IActionResult> Seleccion()
    {
        var selected = await SelectionAsync();
        return Json(new { success = true, limit = FeaturedProductsStore.Limit, items = selected.Select(s => new {
            id = s.Id, productId = s.Product?.Id, name = s.Product?.Nombre ?? "Producto no disponible", category = s.Product?.CategoriaNombre ?? "Sin categoría",
            image = s.Product?.ImagenPrincipal, hidden = s.Product == null || s.Product.Eliminado }) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ordenar(long[] orden, long[] anterior)
    {
        try { await featured.ReorderAsync(orden, anterior); return Json(new { success = true, message = "Orden guardado. Ya se muestra así en el inicio." }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
        catch (Exception error) { return Failed(error); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cambiar(long id, string accion, string busqueda = "", int page = 1, long? categoriaId = null, long? reemplazarId = null, long[]? anterior = null)
    {
        try {
            if (accion == "añadir") {
                var family = (await catalog.FamiliesAsync()).FirstOrDefault(f => f.Members.Any(m => m.Id == id && !m.Eliminado));
                if (family == null) throw new ArgumentException("Solo puedes destacar productos visibles del catálogo.");
                var added = await featured.AddOrReplaceAsync(family.Product.Id, reemplazarId, anterior, family.Members.Select(m => m.Id).ToArray());
                if (!added) {
                    if (Ajax) return Json(new { success = false, needsReplacement = true });
                    throw new ArgumentException("La selección está llena. Elige en el panel qué producto quieres reemplazar.");
                }
            } else await featured.ChangeAsync(id, accion);
            const string message = "Selección actualizada. El cambio ya se muestra en el inicio.";
            if (Ajax) return Json(new { success = true, message });
            TempData["Success"] = message;
        }
        catch (ArgumentException error) { if (Ajax) return BadRequest(new { message = error.Message }); TempData["Warning"] = error.Message; }
        catch (InvalidOperationException error) { if (Ajax) return Conflict(new { message = error.Message }); TempData["Warning"] = error.Message; }
        catch (Exception error) { if (Ajax) return Failed(error); throw; }
        return RedirectToAction(nameof(Index), new { busqueda, page, categoriaId });
    }
    private ObjectResult Failed(Exception error)
    {
        HttpContext.RequestServices.GetRequiredService<ILogger<AdminDestacadosController>>().LogError(error, "No se pudo actualizar la selección de destacados");
        return StatusCode(500, new { message = "No se ha podido confirmar el cambio. Actualiza la selección y vuelve a intentarlo." });
    }
    private async Task<string> RenderAsync(string name, FeaturedProductsViewModel model)
    {
        ViewData.Model = model;
        var view = views.FindView(ControllerContext, name, false);
        if (!view.Success) throw new InvalidOperationException("No se ha encontrado la vista de destacados.");
        using var writer = new StringWriter();
        await view.View.RenderAsync(new ViewContext(ControllerContext, view.View, ViewData, TempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }
}
