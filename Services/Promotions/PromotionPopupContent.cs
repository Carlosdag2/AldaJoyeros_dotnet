using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Services.Promotions;

public static class PromotionPopupContent
{
    public static async Task<object?> CreateAsync(PromotionConfig state, CatalogoService catalog, HttpRequest request)
    {
        var discounted = await catalog.GetAsync(descuentos: "si");
        if (discounted.Count == 0)
            return null;
        var product = state.PopupProductId.HasValue ? discounted.SingleOrDefault(p => p.Id == state.PopupProductId) : null;
        if (state.PopupProductId.HasValue && product == null)
            return null;
        if (product?.TieneTallas == true)
        {
            var size = product.Tallas.Where(t => t.DescuentoPorcentaje > 0 && t.PrecioSinDescuento > t.Precio)
                .OrderBy(t => t.Precio).FirstOrDefault();
            if (size != null)
                product = await catalog.DetailAsync(product.Id, size.ProductoId);
        }

        if (product != null)
            await catalog.LoadImagesAsync(new[] { product });
        var previews = product == null
            ? state.HomeProducts.Select(id => discounted.FirstOrDefault(p => p.Id == id)).OfType<ProductoDto>()
                .Concat(discounted).DistinctBy(p => p.Id).Take(3).ToArray()
            : Array.Empty<ProductoDto>();
        if (previews.Length > 0)
            await catalog.LoadImagesAsync(previews);
        var maxPercent = discounted.SelectMany(p => p.Tallas.Select(t => t.DescuentoPorcentaje)
            .Append(p.DescuentoPorcentaje)).DefaultIfEmpty(0).Max();
        return new
        {
            version = state.PopupVersion, seconds = state.PopupSeconds,
            name = product == null ? null : product.Nombre + (PublicUrls.SelectedSize(product) is { } chosen ? " · Talla " + chosen : ""),
            title = product == null ? "Tu próxima joya, con un precio especial." : "Una pieza especial. Una gran oportunidad.",
            price = product?.Precio.ToString("C"), previous = product?.PrecioSinDescuento?.ToString("C"),
            percent = product?.DescuentoPorcentaje.ToString("0.##"), maxPercent = maxPercent.ToString("0.##"),
            reference = product?.PrecioAnterior != product?.PrecioSinDescuento ? product?.PrecioAnterior?.ToString("C") : null,
            image = product?.ImagenPrincipal, previews = previews.Select(p => new { name = p.Nombre, image = p.ImagenPrincipal }),
            url = product == null ? (request.PathBase + "/catalogo?descuentos=si").ToString() : PublicUrls.ProductLink(request, product)
        };
    }
}
