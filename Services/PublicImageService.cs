using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using MongoDB.Driver;

namespace AldaJoyeros.Services;

// Only metadata is read for catalog HTML; image bytes are requested separately and cached by the browser.
public sealed class PublicImageService(MongoDbContext mongo, IHttpContextAccessor context)
{
    public async Task<IEnumerable<ProductoImagenDto>> GetByProductoIdAsync(long id)
    {
        var images = await mongo.ProductoImagenes.Find(x => x.ProductoId == id).SortBy(x => x.Orden)
            .Project(x => new { x.Id, x.ProductoId, x.Orden, x.EsPrincipal, x.FechaCreacion }).ToListAsync();
        var prefix = context.HttpContext?.Request.PathBase.Value ?? "";
        return images.Select(x => new ProductoImagenDto {
            Id = x.Id, ProductoId = x.ProductoId, Orden = x.Orden, EsPrincipal = x.EsPrincipal,
            FechaCreacion = x.FechaCreacion, TipoMime = "image/webp",
            ImagenBase64 = prefix + "/CatalogoImagen/Foto/" + x.Id + "?v=" + x.FechaCreacion.Ticks
        }).ToArray();
    }
}
