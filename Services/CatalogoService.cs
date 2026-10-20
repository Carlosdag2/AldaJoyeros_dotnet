using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Implementations;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace AldaJoyeros.Services;

// Vista pública de las familias. Los registros del proveedor, administración y pedidos se conservan individuales.
public sealed class CatalogoService(AldaJoyerosContext sql, IMapper mapper, PublicImageService images, CatalogUrlStore urls)
{
    private Task<IReadOnlyList<ProductFamily>>? snapshot;
    public Task<IReadOnlyList<ProductFamily>> FamiliesAsync() => snapshot ??= LoadAsync();
    private async Task<IReadOnlyList<ProductFamily>> LoadAsync()
    {
        var families = ProductFamilies.Build(await sql.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Proveedores)
            .Where(p => !p.Eliminado || p.Proveedores.Any(s => s.Proveedor == "PerezMora" && s.Estado == "completo")).ToListAsync(), p => mapper.Map<ProductoDto>(p));
        string Key(ProductFamily family) => family.Product.TieneTallas ? "familia-pm:" + family.Product.Nombre.ToUpperInvariant() : "producto:" + family.Product.Id;
        var assigned = await urls.GetAsync(families.Select(f => KeyValuePair.Create(Key(f), PublicUrls.ProductSlug(f.Product))));
        foreach (var family in families) family.Product.UrlSlug = CatalogUrlStore.Lookup(assigned, Key(family));
        return families;
    }
    public async Task AssignCategoryUrlsAsync(IEnumerable<CategoriaDto> categories)
    {
        var rows = categories.ToArray();
        var assigned = await urls.GetAsync(rows.Select(c => KeyValuePair.Create("categoria:" + c.Id, PublicUrls.Slug(c.Nombre))));
        foreach (var row in rows) row.UrlSlug = CatalogUrlStore.Lookup(assigned, "categoria:" + row.Id);
    }
    public async Task<ProductoDto?> DetailBySlugAsync(string slug, string? size = null)
    {
        var family = (await FamiliesAsync()).FirstOrDefault(f => f.Product.UrlSlug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        if (family == null) return null;
        long? selected = null;
        if (!string.IsNullOrEmpty(size))
        {
            var matches = family.Product.Tallas.Where(t => t.Talla.Equals(size, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) return null;
            selected = matches[0].ProductoId;
        }
        return await DetailAsync(family.Product.Id, selected);
    }

    public async Task<IReadOnlyList<ProductoDto>> GetAsync(long? categoriaId = null, string? busqueda = null, int limite = 0)
    {
        var families = await FamiliesAsync();
        IEnumerable<ProductFamily> query = families;
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var search = new CatalogSearch(busqueda);
            query = families.Select(f => (Family: f, Score: f.Members.Where(p => !p.Eliminado).Select(p => search.Score(
                    p.Nombre, p.Descripcion, p.Categoria?.Nombre ?? "",
                    p.Proveedores.Where(s => s.Estado == "completo").Select(s => s.ReferenciaExterna).Prepend(p.Nombre),
                    p.Proveedores.Where(s => s.Estado == "completo").SelectMany(s => CatalogSearch.AttributeText(s.CaracteristicasJson)
                        .Concat(new[] { s.DescripcionCompleta, s.Marca, s.Coleccion })))).DefaultIfEmpty(0).Max()))
                .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Family.Product.Id).Select(x => x.Family);
        }
        else if (categoriaId.HasValue)
        {
            var name = await sql.Categorias.AsNoTracking().Where(c => c.Id == categoriaId).Select(c => c.Nombre).FirstOrDefaultAsync();
            query = families.Where(f => f.Members.Any(p => !p.Eliminado && (p.CategoriaId == categoriaId ||
                name != null && !name.Contains(" · ") && p.Categoria != null && p.Categoria.Nombre.StartsWith(name + " · ", StringComparison.Ordinal))));
        }
        if (limite > 0) query = query.Take(limite);
        return query.Select(f => f.Product).ToArray();
    }

    public async Task<ProductoDto?> DetailAsync(long id, long? tallaId = null)
    {
        var family = (await FamiliesAsync()).FirstOrDefault(f => f.Members.Any(p => p.Id == id && !p.Eliminado));
        if (family == null) return null;
        var selectedId = tallaId ?? (id != family.Product.Id && family.Product.TieneTallas ? id : (long?)null);
        var source = family.Members.First(p => p.Id == family.Product.Id);
        if (selectedId.HasValue)
        {
            if (!family.Product.Tallas.Any(t => t.ProductoId == selectedId)) return null;
            source = family.Members.First(p => p.Id == selectedId && !p.Eliminado);
        }
        var dto = mapper.Map<ProductoDto>(source);
        ProductoService.CompletarDatosProveedor(source, dto);
        dto.Id = family.Product.Id;
        dto.Nombre = family.Product.Nombre;
        dto.UrlSlug = family.Product.UrlSlug;
        dto.Tallas = family.Product.Tallas;
        dto.ProductoSeleccionadoId = selectedId;
        dto.Precio = selectedId.HasValue ? source.Precio : family.Product.Precio;
        dto.ReferenciaSeleccionada = selectedId.HasValue ? source.Nombre : null;
        dto.Imagenes = (await images.GetByProductoIdAsync(source.Id)).ToList();
        if (!dto.TieneImagenes) await LoadImagesAsync(new[] { dto });
        // Las características de talla y peso corresponden solo a la variante elegida.
        if (dto.TieneTallas && !selectedId.HasValue)
            dto.Caracteristicas.RemoveAll(c => c.Nombre.Equals("TALLA", StringComparison.OrdinalIgnoreCase) || c.Nombre.Equals("PESO G.", StringComparison.OrdinalIgnoreCase));
        return dto;
    }

    public async Task LoadImagesAsync(IEnumerable<ProductoDto> products)
    {
        var families = (await FamiliesAsync()).ToDictionary(f => f.Product.Id);
        foreach (var dto in products)
        {
            if (dto.TieneImagenes) continue;
            var ids = families.TryGetValue(dto.Id, out var f)
                ? f.Members.Where(p => !p.Eliminado).OrderBy(p => p.Id == dto.Id ? 0 : 1).Select(p => p.Id)
                : new[] { dto.Id };
            foreach (var imageId in ids)
            {
                dto.Imagenes = (await images.GetByProductoIdAsync(imageId)).ToList();
                if (dto.TieneImagenes) break;
            }
        }
    }

    public async Task<IReadOnlyList<ProductoDto>> RelatedAsync(ProductoDto product)
    {
        var related = (await GetAsync()).Where(p => p.Id != product.Id && p.CategoriaId == product.CategoriaId)
            .OrderBy(p => Math.Abs(p.Precio - product.Precio)).ThenBy(p => p.Id).Take(4).ToArray();
        await LoadImagesAsync(related);
        return related;
    }

    public async Task<bool> IsPurchasableAsync(long id)
    {
        var product = await sql.Productos.AsNoTracking().Include(p => p.Proveedores).FirstOrDefaultAsync(p => p.Id == id && !p.Eliminado);
        if (product == null) return false;
        if (ProductFamilies.Parse(product).HasValue) return true;
        if (!product.Proveedores.Any(s => s.Proveedor == "PerezMora" && s.Estado == "completo")) return true;
        var prefix = product.Nombre + "/";
        var children = await sql.Productos.AsNoTracking().Include(p => p.Proveedores).Where(p => p.Nombre.StartsWith(prefix)).ToListAsync();
        return !children.Any(p => ProductFamilies.Parse(p).HasValue);
    }
}
