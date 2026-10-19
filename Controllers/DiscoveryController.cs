using System.Xml.Linq;
using AldaJoyeros.Configuration;
using AldaJoyeros.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Controllers;

public sealed class DiscoveryController(IOptions<PublicationOptions> publication) : Controller
{
    private const int ChunkSize = 5000;
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var p = publication.Value;
        var prefix = p.PublicOrigin == null ? "" : new Uri(p.PublicOrigin).AbsolutePath.TrimEnd('/');
        var exclusions = string.Join("", new[] { "/Auth", "/Admin", "/Pedidos", "/Carrito" }.Select(path => "Disallow: " + prefix + path + "\n"));
        return Content(p.CanIndex ? "User-agent: *\nAllow: /\n" + exclusions + "Sitemap: " + p.PublicOrigin + "/sitemap.xml\n"
            : "User-agent: *\nDisallow: /\n", "text/plain; charset=utf-8");
    }
    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        if (!publication.Value.CanIndex) return NotFound();
        var count = (await HttpContext.RequestServices.GetRequiredService<CatalogoService>().GetAsync()).Count;
        var chunks = (int)Math.Ceiling(count / (double)ChunkSize);
        return Xml(new XElement(Ns + "sitemapindex", Enumerable.Range(0, chunks + 1).Select(i =>
            new XElement(Ns + "sitemap", new XElement(Ns + "loc", publication.Value.PublicOrigin + $"/sitemap-{i}.xml")))));
    }
    [HttpGet("sitemap-{page:int}.xml")]
    public async Task<IActionResult> Page(int page)
    {
        if (!publication.Value.CanIndex || page < 0) return NotFound();
        IEnumerable<string> paths;
        if (page == 0) paths = new[] { "/", "/Productos", "/Home/AvisoLegal", "/Home/Privacy", "/Home/Cookies", "/Home/Terminos" };
        else
        {
            var catalog = await HttpContext.RequestServices.GetRequiredService<CatalogoService>().GetAsync();
            if (catalog.Count == 0 || page > (int)Math.Ceiling(catalog.Count / (double)ChunkSize)) return NotFound();
            var products = catalog.OrderBy(p => p.Id).Skip((page - 1) * ChunkSize).Take(ChunkSize).ToArray();
            if (products.Length == 0) return NotFound();
            paths = products.Select(p => "/Productos/Detalle/" + p.Id);
        }
        return Xml(new XElement(Ns + "urlset", paths.Select(path => new XElement(Ns + "url", new XElement(Ns + "loc", publication.Value.PublicOrigin + path)))));
    }
    private ContentResult Xml(XElement content) => Content(new XDocument(new XDeclaration("1.0", "utf-8", null), content).ToString(), "application/xml; charset=utf-8");
}
