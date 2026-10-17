using AldaJoyeros.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AldaJoyeros.Controllers;

// Las fotos de piezas visibles son datos del catálogo público. No se expone información de administración.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CatalogoPantallaController(AldaJoyerosContext sql, MongoDbContext mongo) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Imagen(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out _)) return NotFound();
        var image = await mongo.ProductoImagenes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (image == null || image.ImagenData.Length == 0 || !image.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return NotFound();
        if (!await sql.Productos.AsNoTracking().AnyAsync(p => p.Id == image.ProductoId && !p.Eliminado, ct)) return NotFound();
        return File(image.ImagenData, image.TipoMime);
    }
}
