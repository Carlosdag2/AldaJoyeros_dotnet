using System.Globalization;
using System.Text;
using AldaJoyeros.Configuration;
using AldaJoyeros.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace AldaJoyeros.Helpers;

public static class PublicUrls
{
    public static readonly IReadOnlyDictionary<string,string> HomePaths = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Index"] = "/", ["AvisoLegal"] = "/aviso-legal", ["Privacy"] = "/privacidad",
        ["Cookies"] = "/cookies", ["Terminos"] = "/condiciones-de-compra"
    };
    public static string ProductSlug(ProductoDto product)
    {
        var slug = string.IsNullOrEmpty(product.UrlSlug) ? Slug(product.CategoriaNombre + "-" + product.Nombre) : product.UrlSlug;
        return slug.All(char.IsAsciiDigit) ? "pieza-" + slug : slug;
    }
    public static string Slug(string text)
    {
        var value = text.Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var result = new StringBuilder();
        foreach (var character in value)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9') result.Append(character);
            else if (result.Length > 0 && result[^1] != '-') result.Append('-');
            if (result.Length >= 140) break;
        }
        var slug = result.ToString().Trim('-');
        return slug.Length == 0 ? "joya" : slug;
    }
    public static string ProductPath(ProductoDto product) => "/producto/" + ProductSlug(product);
    public static string ProductLink(HttpRequest request, ProductoDto product)
    {
        var path = (request.PathBase + ProductPath(product)).ToString();
        return SelectedSize(product) is { } size ? QueryHelpers.AddQueryString(path, "talla", size) : path;
    }
    public static string CatalogPath(string? category = null, int page = 1)
    {
        var path = string.IsNullOrEmpty(category) ? "/catalogo" : "/catalogo/" + category;
        if (page > 1) path = QueryHelpers.AddQueryString(path, "page", page.ToString(CultureInfo.InvariantCulture));
        return path;
    }
    // A configured domain is trusted configuration; request headers never decide the canonical host.
    public static string? Canonical(PublicationOptions options, string logicalPath) => options.PublicOrigin is { } origin ? origin + logicalPath : null;

    public static bool ShouldRedirectOrigin(HttpRequest request, PublicationOptions options)
    {
        if (!options.RedirectToPublicOrigin || !options.CanIndex) return false;
        var configured = new Uri(options.PublicOrigin!);
        return !request.Scheme.Equals(configured.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Host.Value, configured.Authority, StringComparison.OrdinalIgnoreCase);
    }
    public static string Destination(HttpRequest request, PublicationOptions options, string logicalPath, string query)
        => (ShouldRedirectOrigin(request, options) ? options.PublicOrigin : request.PathBase.Value ?? "") + logicalPath + query;

    public static string? MemberLink(HttpRequest request, IEnumerable<ProductFamily> families, long id)
    {
        var family = families.FirstOrDefault(f => f.Members.Any(p => p.Id == id && !p.Eliminado));
        if (family == null) return null;
        var path = (request.PathBase + ProductPath(family.Product)).ToString();
        return family.Product.Tallas.FirstOrDefault(t => t.ProductoId == id) is { } size
            ? QueryHelpers.AddQueryString(path, "talla", size.Talla) : path;
    }
    public static string? SelectedSize(ProductoDto product) => product.Tallas.FirstOrDefault(t => t.ProductoId == product.ProductoSeleccionadoId)?.Talla;
    public static string CatalogQuery(HttpRequest request, string search, int page)
    {
        var query = QueryHelpers.ParseQuery(request.QueryString.Value);
        query.Remove("categoriaId"); query.Remove("categoria"); query.Remove("page"); query.Remove("busqueda");
        if (page > 1) query["page"] = page.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(search)) query["busqueda"] = search.Trim();
        return QueryHelpers.AddQueryString("", query);
    }
    public static string ProductQuery(HttpRequest request, ProductoDto product)
    {
        var query = QueryHelpers.ParseQuery(request.QueryString.Value);
        query.Remove("id"); query.Remove("slug"); query.Remove("tallaId"); query.Remove("talla");
        if (SelectedSize(product) is { } size) query["talla"] = size;
        return QueryHelpers.AddQueryString("", query);
    }
}
