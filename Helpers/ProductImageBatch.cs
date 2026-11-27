using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;
namespace AldaJoyeros.Helpers;

public static class ProductImageBatch
{
    public static async Task LoadAsync(IProductoImagenService images, IEnumerable<(long Id, ProductoDto Product)> products)
    {
        var rows = products.ToArray();
        if (rows.Length == 0) return;
        var grouped = (await images.GetByProductoIdsAsync(rows.Select(p => p.Id))).ToLookup(i => i.ProductoId);
        foreach (var row in rows) row.Product.Imagenes = grouped[row.Id].ToList();
    }
}
