using AldaJoyeros.Configuration;
using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AldaJoyeros.Services;

// Read metadata only. Original bytes remain in MongoDB and are never changed here.
public sealed class PublicImageService(MongoDbContext mongo, IHttpContextAccessor context,
    IOptions<StorageOptimizationOptions>? optimization = null)
{
    // Scoped to this request: edits/deletions are visible in the next request.
    readonly Dictionary<long, IReadOnlyList<ProductoImagenDto>> metadata = new();
    bool Optimized => optimization?.Value.Enabled ?? true;

    public async Task<IEnumerable<ProductoImagenDto>> GetByProductoIdAsync(long id) =>
        (await GetByProductoIdsAsync(new[] { id }))[id];

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<ProductoImagenDto>>> GetByProductoIdsAsync(IEnumerable<long> productIds)
    {
        var ids = productIds.Distinct().ToArray();
        if (!Optimized)
        {
            var legacy = new Dictionary<long, IReadOnlyList<ProductoImagenDto>>();
            foreach (var id in ids) legacy[id] = await QueryAsync(new[] { id });
            return legacy;
        }
        // Bounded queries also handle large families without building an unbounded $in.
        foreach (var batch in ids.Where(id => !metadata.ContainsKey(id)).Chunk(200))
        {
            var rows = await QueryAsync(batch);
            var groups = rows.ToLookup(image => image.ProductoId);
            foreach (var id in batch) metadata[id] = groups[id].ToArray();
        }
        return ids.ToDictionary(id => id, id => metadata[id]);
    }

    async Task<IReadOnlyList<ProductoImagenDto>> QueryAsync(long[] ids)
    {
        var filter = ids.Length == 1
            ? Builders<AldaJoyeros.Models.MongoDB.ProductoImagenMongo>.Filter.Eq(x => x.ProductoId, ids[0])
            : Builders<AldaJoyeros.Models.MongoDB.ProductoImagenMongo>.Filter.In(x => x.ProductoId, ids);
        var rows = await mongo.ProductoImagenes.Find(filter).SortBy(x => x.ProductoId).ThenBy(x => x.Orden)
            .Project(x => new { x.Id, x.ProductoId, x.Orden, x.EsPrincipal, x.FechaCreacion })
            .ToListAsync(context.HttpContext?.RequestAborted ?? default);
        var prefix = context.HttpContext?.Request.PathBase.Value ?? "";
        return rows.Select(x => new ProductoImagenDto {
            Id = x.Id, ProductoId = x.ProductoId, Orden = x.Orden, EsPrincipal = x.EsPrincipal,
            FechaCreacion = x.FechaCreacion, TipoMime = "image/webp",
            ImagenBase64 = prefix + "/CatalogoImagen/Foto/" + x.Id + "?v=" + x.FechaCreacion.Ticks
        }).ToArray();
    }
}
