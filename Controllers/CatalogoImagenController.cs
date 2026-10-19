using System.Security.Cryptography;
using AldaJoyeros.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AldaJoyeros.Controllers;

public sealed class CatalogoImagenController(AldaJoyerosContext sql, MongoDbContext mongo, AldaJoyeros.Services.PublicImageCache cache) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Foto(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return NotFound();
        var image = await mongo.ProductoImagenes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (image == null || image.ImagenData.Length == 0 || image.ImagenData.Length > 20 * 1024 * 1024) return NotFound();
        if (!await sql.Productos.AsNoTracking().AnyAsync(p => p.Id == image.ProductoId && !p.Eliminado, ct)) return NotFound();
        Response.Headers.CacheControl = "public, max-age=300";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var etag = "\"webp800-v1-" + Convert.ToHexString(SHA256.HashData(image.ImagenData)) + "\"";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch.Any(value => value != null && value.Split(',').Any(tag => tag.Trim().TrimStart('W', '/') == etag)))
            return StatusCode(304);
        if (cache.TryGet(etag, out var cached)) return File(cached!, "image/webp");
        await cache.ConversionSlots.WaitAsync(ct);
        try
        {
            if (cache.TryGet(etag, out cached)) return File(cached!, "image/webp");
            var bytes = AldaJoyeros.Services.PublicImageEncoder.Encode(image.ImagenData);
            if (bytes == null) return NotFound();
            cache.Store(etag, bytes);
            return File(bytes, "image/webp");
        }
        finally { cache.ConversionSlots.Release(); }

    }
}
