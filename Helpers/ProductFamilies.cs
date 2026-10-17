using System.Globalization;
using System.Text.RegularExpressions;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;

namespace AldaJoyeros.Helpers;

public sealed record ProductFamily(ProductoDto Product, IReadOnlyList<Producto> Members);

public static class ProductFamilies
{
    // Solo Pérez Mora utiliza esta convención de tallas. Una barra en otro proveedor no implica una talla.
    private static readonly Regex SizeReference = new(@"^([A-Za-z0-9.-]+)/([0-9]+(?:[.,][0-9]+)?)$", RegexOptions.CultureInvariant);

    public static (string Parent, string Size)? Parse(Producto product)
    {
        if (!product.Proveedores.Any(p => p.Proveedor == "PerezMora" && p.Estado == "completo")) return null;
        var match = SizeReference.Match(product.Nombre.Trim());
        return match.Success && decimal.TryParse(match.Groups[2].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out _) ? (match.Groups[1].Value, match.Groups[2].Value) : null;
    }

    public static IReadOnlyList<ProductFamily> Build(IEnumerable<Producto> products, Func<Producto, ProductoDto> map)
    {
        var rows = products.ToArray();
        var parents = rows.Where(p => Parse(p).HasValue).Select(p => Parse(p)!.Value.Parent)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string Key(Producto p)
        {
            var size = Parse(p);
            if (size.HasValue) return "pm:" + size.Value.Parent.ToUpperInvariant();
            if (parents.Contains(p.Nombre) && p.Proveedores.Any(s => s.Proveedor == "PerezMora" && s.Estado == "completo"))
                return "pm:" + p.Nombre.ToUpperInvariant();
            return "id:" + p.Id;
        }
        var result = new List<ProductFamily>();
        foreach (var group in rows.GroupBy(Key))
        {
            var members = group.OrderBy(p => p.Id).ToArray();
            var children = members.Where(p => Parse(p).HasValue).ToArray();
            if (children.Length == 0)
            {
                foreach (var p in members.Where(p => !p.Eliminado)) result.Add(new(map(p), new[] { p }));
                continue;
            }
            var available = children.Where(p => !p.Eliminado).ToArray();
            if (available.Length == 0) continue; // El padre no es una talla comprable.
            var parent = members.FirstOrDefault(p => !Parse(p).HasValue);
            var representative = parent is { Eliminado: false } ? parent : available[0];
            var dto = map(representative);
            dto.Nombre = Parse(children[0])!.Value.Parent;
            dto.Tallas = available.Select(p => new ProductoTallaDto(p.Id, p.Nombre, Parse(p)!.Value.Size, p.Precio))
                .OrderBy(p => decimal.Parse(p.Talla.Replace(',', '.'), CultureInfo.InvariantCulture)).ThenBy(p => p.ProductoId).ToList();
            dto.Precio = dto.Tallas.Min(p => p.Precio);
            result.Add(new(dto, members));
        }
        return result.OrderBy(f => f.Product.Id).ToArray();
    }
}
