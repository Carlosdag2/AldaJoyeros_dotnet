using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AldaJoyeros.Attributes;
using AldaJoyeros.Helpers;
using AldaJoyeros.Models;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Promotions;
using AldaJoyeros.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;
[JwtAuthorize("ADMIN")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDescuentosController(PromotionStore store, CatalogoService catalog, ICategoriaService categories, IOptions<PublicationOptions> publication) : BaseController
{
    public async Task<IActionResult> Index(string search = "", long? categoryId = null, int page = 1)
    {
        var all = await catalog.GetAsync();
        var config = await store.GetAsync();
        var cats = (await categories.GetAllAsync()).OrderBy(c => c.Nombre).ToArray();
        var filtered = all.Where(p => (string.IsNullOrWhiteSpace(search) || p.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase) || p.CategoriaNombre.Contains(search, StringComparison.OrdinalIgnoreCase)) && (!categoryId.HasValue || p.CategoriaId == categoryId || cats.Any(c => c.Id == categoryId && !c.Nombre.Contains(" · ") && p.CategoriaNombre.StartsWith(c.Nombre + " · ", StringComparison.Ordinal)))).ToArray();
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / 24d));
        page = Math.Clamp(page, 1, pages);
        var rows = filtered.Skip((page - 1) * 24).Take(24).ToArray();
        var selected = all.Where(p => config.HomeProducts.Contains(p.Id) || config.PopupProductId == p.Id).ToArray();
        await catalog.LoadImagesAsync(rows.Concat(selected));
        var vm = new PromotionsAdminViewModel(config, rows, selected, cats, search, categoryId, page, pages, filtered.Length, await store.AudienceAsync(), await store.CampaignsAsync());
        return Request.Headers.XRequestedWith == "XMLHttpRequest" ? PartialView("_Panel", vm) : View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> VistaAviso(long? productId = null)
    {
        // GetAsync returns an independent configuration; a preview never saves it.
        var state = await store.GetAsync();
        state.PopupProductId = productId;
        var content = await PromotionPopupContent.CreateAsync(state, catalog, Request);
        if (content == null)
            return Content("No hay una oferta disponible para esta vista previa.", "text/plain");
        ViewBag.PopupPreview = content;
        return View("VistaAviso");
    }

    [HttpGet]
    public async Task<IActionResult> Elegir(string search = "", long? categoryId = null, int page = 1, bool onlyDiscounted = true)
    {
        var all = await catalog.GetAsync(descuentos: onlyDiscounted ? "si" : null);
        var category = categoryId.HasValue
            ? (await categories.GetAllAsync()).FirstOrDefault(c => c.Id == categoryId) : null;
        if (categoryId.HasValue && category == null)
            return BadRequest(new { message = "La categoría ya no está disponible." });
        var term = search.Trim();
        var filtered = all.Where(p =>
            (term.Length == 0 || p.Nombre.Contains(term, StringComparison.OrdinalIgnoreCase) || p.CategoriaNombre.Contains(term, StringComparison.OrdinalIgnoreCase)) &&
            (!categoryId.HasValue || p.CategoriaId == categoryId ||
                !category!.Nombre.Contains(" · ") && p.CategoriaNombre.StartsWith(category.Nombre + " · ", StringComparison.Ordinal))).ToArray();
        var pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / 12d));
        page = Math.Clamp(page, 1, pages);
        var rows = filtered.Skip((page - 1) * 12).Take(12).ToArray();
        await catalog.LoadImagesAsync(rows);
        return Json(new
        {
            success = true, total = filtered.Length, page, totalPages = pages,
            items = rows.Select(p => new
            {
                id = p.Id, name = p.Nombre, category = p.CategoriaNombre, image = p.ImagenPrincipal,
                price = (p.PrecioDesde ? "Desde " : "") + p.Precio.ToString("C"),
                percent = p.DescuentoPorcentaje.ToString("0.##"), discounted = p.TieneDescuento
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> Producto(long id) {
        var family=(await catalog.FamiliesAsync()).FirstOrDefault(f=>f.Product.Id==id);
        if(family==null)return NotFound(new{message="El producto ya no está disponible."});
        var config=await store.GetAsync();var direct=config.Rules.FirstOrDefault(r=>r.ProductId==id);
        var all=await catalog.GetAsync();var selected=config.HomeProducts.Select(key=>all.FirstOrDefault(p=>p.Id==key)).OfType<AldaJoyeros.DTOs.ProductoDto>().ToArray();
        await catalog.LoadImagesAsync(selected);
        return Json(new{success=true,revision=config.Revision,percent=direct?.Percent??0,ruleEnabled=direct?.Enabled??false,discounted=family.Product.TieneDescuento,homeSelected=config.HomeProducts.Contains(id),homeEnabled=config.HomeEnabled,limit=8,
            selection=config.HomeProducts.Select(key=>new{id=key,name=selected.FirstOrDefault(p=>p.Id==key)?.Nombre??"Producto no disponible",image=selected.FirstOrDefault(p=>p.Id==key)?.ImagenPrincipal})});
    }
    [HttpPost]
    public async Task<IActionResult> DescuentoProducto(long revision,long productId,string percent,bool confirmed=false) {
        if(!decimal.TryParse(percent?.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var rate))return BadRequest(new{message="Indica un porcentaje válido."});
        var state=await store.GetAsync();var direct=state.Rules.FirstOrDefault(r=>r.ProductId==productId);
        if(rate==0){try{await store.ChangeAsync(revision,c=>c.Rules.RemoveAll(r=>r.ProductId==productId));return Json(new{success=true,message="Descuento individual retirado. Si existe una oferta de categoría, se mantiene."});}catch(InvalidOperationException e){return Conflict(new{message=e.Message});}}
        return await Rule(revision,direct?.Id,"Descuento de producto",percent,null,productId,confirmed);
    }
    [HttpPost]
    public async Task<IActionResult> InicioProducto(long revision,long productId,bool remove=false,long? replaceId=null) {
        try {
            var product=(await catalog.GetAsync()).FirstOrDefault(p=>p.Id==productId);
            if(!remove&&product?.TieneDescuento!=true)throw new ArgumentException("Primero aplica un descuento que pueda anunciarse como oferta.");
            await store.ChangeAsync(revision,c=>{
                if(remove){c.HomeProducts.Remove(productId);if(c.HomeProducts.Count==0)c.HomeEnabled=false;return;}
                if(!c.HomeProducts.Contains(productId)){
                    if(c.HomeProducts.Count>=8){if(!replaceId.HasValue||!c.HomeProducts.Contains(replaceId.Value))throw new ArgumentException("Elige la oferta que quieres reemplazar.");c.HomeProducts[c.HomeProducts.IndexOf(replaceId.Value)]=productId;}
                    else c.HomeProducts.Add(productId);
                }
                c.HomeEnabled=true;
            });
            return Json(new{success=true,message=remove?"Producto quitado de las ofertas del inicio.":"Producto añadido a las ofertas del inicio. La sección está activada."});
        }catch(Exception e)when(e is ArgumentException or InvalidOperationException){return Conflict(new{message=e.Message});}
    }
    [HttpPost]
    public async Task<IActionResult> Rule(long revision, string? id, string? name, string percent, long? categoryId, long? productId, bool confirmed = false)
    {
        try
        {
            name=string.IsNullOrWhiteSpace(name)?"Descuento "+percent+" %":name.Trim();
            if (name.Length > 80 || !decimal.TryParse(percent?.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate < 0.01m || rate >= 100 || rate != decimal.Round(rate, 2))
                throw new ArgumentException("Indica un nombre y un porcentaje mayor que 0 y menor que 100.");
            if (categoryId.HasValue == productId.HasValue)
                throw new ArgumentException("Elige una categoría o un producto.");
            if (!confirmed)
                throw new ArgumentException("Confirma que los precios de referencia son los menores aplicados durante los 30 días anteriores.");
            var families = await catalog.FamiliesAsync();
            var category = categoryId.HasValue ? (await categories.GetAllAsync()).SingleOrDefault(c => c.Id == categoryId) : null;
            var family = productId.HasValue ? families.FirstOrDefault(f => f.Product.Id == productId) : null;
            if (categoryId.HasValue && category == null || productId.HasValue && family == null)
                throw new ArgumentException("El producto o categoría ya no está disponible.");
            var members = (family != null ? family.Members : families.SelectMany(f => f.Members).Where(p => p.CategoriaId == categoryId || category != null && !category.Nombre.Contains(" · ") && p.Categoria?.Nombre.StartsWith(category.Nombre + " · ", StringComparison.Ordinal) == true)).Where(p => !p.Eliminado).DistinctBy(p => p.Id).ToArray();
            if (members.Length == 0)
                throw new ArgumentException("La selección no contiene productos disponibles.");
            var refs = await store.ReferencesAsync();
            await store.ChangeAsync(revision, state =>
            {
                var old = id == null ? state.Rules.FirstOrDefault(r => productId.HasValue ? r.ProductId == productId : r.CategoryId == categoryId) : state.Rules.SingleOrDefault(r => r.Id == id);
                if (id != null && old == null)
                    throw new ArgumentException("El descuento ya no existe.");
                if (old == null && state.Rules.Count >= 100)
                    throw new ArgumentException("Puedes tener hasta 100 descuentos. Elimina uno que ya no utilices.");
                var references = members.ToDictionary(p => p.Id, p => Math.Min((decimal)p.Precio, refs.GetValueOrDefault(p.Id, (decimal)p.Precio)));
                if (old != null && old.CategoryId == categoryId && old.ProductId == productId && old.Percent == rate)
                    references = old.References;
                var rule = new DiscountRule
                {
                    Id = old?.Id ?? Guid.NewGuid().ToString("N"),
                    Name = name.Trim(),
                    Percent = rate,
                    CategoryId = categoryId,
                    CategoryName = category?.Nombre ?? "",
                    ProductId = productId,
                    FamilyReference = family?.Product.TieneTallas == true ? family.Product.Nombre : "",
                    References = references,
                    PreviousPricesConfirmed = true
                };
                if (old != null)
                    state.Rules.Remove(old);
                state.Rules.RemoveAll(r => productId.HasValue ? r.ProductId == productId : r.CategoryId == categoryId);
                state.Rules.Add(rule);
            });
            return Json(new { success = true, message = "Descuento guardado. Ya se aplica en la web." });
        }
        catch (Exception e)when (e is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(long revision, string id, bool remove = false)
    {
        try
        {
            await catalog.FamiliesAsync();
            var refs = await store.ReferencesAsync();
            await store.ChangeAsync(revision, state =>
            {
                var rule = state.Rules.SingleOrDefault(r => r.Id == id) ?? throw new ArgumentException("El descuento ya no existe.");
                if (remove)
                    state.Rules.Remove(rule);
                else
                {
                    if (!rule.Enabled)
                        rule.References = rule.References.ToDictionary(p => p.Key, p => Math.Min(p.Value, refs.GetValueOrDefault(p.Key, p.Value)));
                    rule.Enabled = !rule.Enabled;
                }
            });
            return Json(new { success = true, message = remove ? "Descuento eliminado." : "Estado del descuento actualizado." });
        }
        catch (Exception e)when (e is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Presentation(long revision, bool homeEnabled, string? homeProducts, bool popupEnabled, long? popupProductId, int popupSeconds = 10)
    {
        try
        {
            var ids = Ids(homeProducts);
            if (ids.Count > 8 || popupSeconds is < 5 or > 60)
                throw new ArgumentException("Elige hasta 8 productos y un aviso de entre 5 y 60 segundos.");
            var all = await catalog.GetAsync();
            if (ids.Any(id => !all.Any(p => p.Id == id && p.TieneDescuento)) || popupEnabled && popupProductId.HasValue && !all.Any(p => p.Id == popupProductId && p.TieneDescuento))
                throw new ArgumentException("Solo puedes promocionar productos disponibles con descuento. Revisa la selección.");
            await store.ChangeAsync(revision, state =>
            {
                state.HomeEnabled = homeEnabled;
                state.HomeProducts = ids;
                state.PopupEnabled = popupEnabled;
                state.PopupProductId = popupProductId;
                state.PopupSeconds = popupSeconds;
                state.PopupVersion = Guid.NewGuid().ToString("N");
            });
            return Json(new { success = true, message = "Presentación de ofertas guardada." });
        }
        catch (Exception e)when (e is ArgumentException or InvalidOperationException or FormatException)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    static List<long> Ids(string? text)
    {
        var parts = (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 8 || parts.Any(x => !long.TryParse(x, out var id) || id <= 0))
            throw new ArgumentException("Selección de productos inválida.");
        return parts.Select(long.Parse).Distinct().ToList();
    }

    async Task<List<PromotionEmailProduct>> EmailProducts(string? productIds)
    {
        var ids = Ids(productIds);
        var all = await catalog.GetAsync(descuentos: "si");
        var rows = ids.Count == 0 ? all.Take(4).ToArray() : ids.Select(id => all.SingleOrDefault(p => p.Id == id) ?? throw new ArgumentException("Uno de los productos ya no tiene descuento.")).ToArray();
        if (rows.Length == 0)
            throw new ArgumentException("No hay productos con descuento para anunciar.");
        var advertised = new List<AldaJoyeros.DTOs.ProductoDto>();
        foreach (var row in rows)
        {
            if (row.TieneTallas)
            {
                var size = row.Tallas.Where(t => t.DescuentoPorcentaje > 0 && t.PrecioSinDescuento > t.Precio).OrderBy(t => t.Precio).FirstOrDefault();
                if (size != null)
                {
                    var selected = await catalog.DetailAsync(row.Id, size.ProductoId);
                    if (selected != null)
                    {
                        advertised.Add(selected);
                        continue;
                    }
                }
            }

            advertised.Add(row);
        }

        await catalog.LoadImagesAsync(advertised);
        var origin = Origin();
        return advertised.Select(p => new PromotionEmailProduct(p.Id, p.ProductoSeleccionadoId, p.Nombre + (PublicUrls.SelectedSize(p)is { } size ? " · Talla " + size : ""), origin + PublicUrls.ProductPath(p) + (PublicUrls.SelectedSize(p)is { } chosen ? "?talla=" + Uri.EscapeDataString(chosen) : ""), p.ImagenPrincipal is { } image && image.StartsWith('/') ? origin + (Request.PathBase.HasValue && image.StartsWith(Request.PathBase + "/", StringComparison.Ordinal) ? image[Request.PathBase.Value!.Length..] : image) : null, (decimal)p.Precio, (decimal)(p.PrecioAnterior ?? p.Precio), p.DescuentoPorcentaje) { BasePrice=(decimal?)p.PrecioSinDescuento }).ToList();
    }

    string Origin() => publication.Value.PublicOrigin ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
    static string Digest(List<PromotionEmailProduct> products) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(products))));
    [HttpPost]
    public async Task<IActionResult> Preview(string subject, string? productIds)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subject) || subject.Length > 160)
                throw new ArgumentException("Escribe un asunto de hasta 160 caracteres.");
            var products = await EmailProducts(productIds);
            return Json(new { success = true, html = PromotionMail.Render(subject, Origin(), products, "vista-previa"), digest = Digest(products), audience = await store.AudienceAsync(), campaignId = Guid.NewGuid().ToString("N") });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Send(string subject, string? productIds, string digest, string campaignId)
    {
        try
        {
            var products = await EmailProducts(productIds);
            if (Digest(products) != digest)
                throw new ArgumentException("Los precios o productos han cambiado. Revisa otra vez la vista previa.");
            await store.QueueAsync(campaignId, subject, Origin(), products);
            return Json(new { success = true, message = "Campaña preparada. Se enviará únicamente a los usuarios suscritos; puedes seguir su progreso aquí." });
        }
        catch (Exception e)when (e is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
    }
}
