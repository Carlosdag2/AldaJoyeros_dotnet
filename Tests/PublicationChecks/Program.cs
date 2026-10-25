using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using AldaJoyeros.Configuration;
using AldaJoyeros.Controllers;
using AldaJoyeros.DTOs;
using AldaJoyeros.Extensions;
using AldaJoyeros.Helpers;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Implementations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using SkiaSharp;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
Check(!new PublicationOptions().CanIndex, "La indexación está desactivada por defecto");
foreach (var invalid in new[] { "http://tienda.example", "https://localhost:7155", "https://user:secret@tienda.example", "https://tienda.example/?token=x" })
    Check(!new PublicationOptions { IndexingEnabled = true, PublicBaseUrl = invalid }.CanIndex, "Dirección pública insegura o inválida descartada");
Check(new PublicationOptions { IndexingEnabled = true, PublicBaseUrl = "https://tienda.example/joyeria/" }.PublicOrigin == "https://tienda.example/joyeria", "La subcarpeta pública se conserva");
Check(PublicImageEncoder.Encode(new byte[] { 1, 2, 3 }) == null, "Una imagen inválida no se publica");
using (var original = new SKBitmap(1600, 1000))
{
    original.Erase(SKColors.Gold);
    using var png = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 100);
    var bytes = PublicImageEncoder.Encode(png.ToArray());
    using var webp = SKCodec.Create(new SKMemoryStream(bytes!));
    Check(webp != null && webp.EncodedFormat == SKEncodedImageFormat.Webp && webp.Info.Width == 800 && webp.Info.Height == 500, "WebP conserva la proporción y limita el lado mayor a 800 px");
}


// Price ranges are inclusive, accept Spanish decimals, and compare the price shown to cents.
Check(CatalogPriceRange.TryCreate(null, "", out var unrestricted) && unrestricted.Contains(123.45), "Sin límites se conserva el catálogo");
Check(CatalogPriceRange.TryCreate("50,25", "100.50", out var range) && range.Contains(50.25) && range.Contains(100.50), "Precios mínimo y máximo inclusivos con coma o punto");
Check(!range.Contains(50.24) && !range.Contains(100.51), "Se excluyen precios fuera del rango");
Check(range.Contains(100.500000000001), "Se comparan importes monetarios redondeados a céntimos");
Check(CatalogPriceRange.TryCreate("0", null, out var zero) && zero.Contains(0), "El mínimo cero es un filtro válido");
Check(CatalogPriceRange.TryCreate(null, "10", out var maximum) && maximum.Contains(10) && !maximum.Contains(10.01), "Se permite solo un máximo");
Check(CatalogPriceRange.TryCreate("10", null, out var minimum) && minimum.Contains(999) && !minimum.Contains(9.99), "Se permite solo un mínimo");
foreach (var bad in new[] { "-1", "NaN", "Infinity", "1.001", "texto", "99999999999999999999999999999999999", "1,000.00" })
    Check(!CatalogPriceRange.TryCreate(bad, null, out _), "Se rechaza un precio inválido: " + bad);
Check(!CatalogPriceRange.TryCreate("100", "50", out _), "Se rechazan límites invertidos");
Check(CatalogPriceRange.TryCreate("25", "25", out var exact) && exact.Contains(25) && !exact.Contains(25.01), "Se permite buscar un precio exacto");
Check(!unrestricted.Contains(double.NaN) && !unrestricted.Contains(double.PositiveInfinity), "Precios no finitos no coinciden");

var ring = new ProductoDto { Id = 2792, Nombre = "02000090", CategoriaNombre = "Anillos · Oro de 18 quilates" };
Check(PublicUrls.ProductPath(ring) == "/producto/anillos-oro-de-18-quilates-02000090", "URL descriptiva conserva referencia y ceros iniciales");
Check(PublicUrls.ProductSlug(new ProductoDto { Nombre = "A/21 <script>", CategoriaNombre = "Colección de señora" }) == "coleccion-de-senora-a-21-script", "El slug elimina acentos, barras y caracteres de marcado");
var request = new DefaultHttpContext().Request;
request.PathBase = "/joyeria"; request.Scheme = "https"; request.Host = new HostString("portatil.example.ts.net");
ring.ProductoSeleccionadoId = 3001;
ring.Tallas.Add(new ProductoTallaDto(3001, "02000090/21", "21", 120));
Check(PublicUrls.ProductLink(request, ring) == "/joyeria/producto/anillos-oro-de-18-quilates-02000090?talla=21", "El enlace conserva subcarpeta y talla seleccionada");
request.QueryString = new QueryString("?id=3001&slug=antiguo&tallaId=3002&utm_source=correo");
var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(PublicUrls.ProductQuery(request, ring));
Check(query["talla"] == "21" && !query.ContainsKey("tallaId") && query["utm_source"] == "correo" && !query.ContainsKey("id") && !query.ContainsKey("slug"), "La migración conserva talla y seguimiento sin duplicar parámetros de ruta");
var settings = new PublicationOptions { IndexingEnabled = true, PublicBaseUrl = "https://tienda.example/joyeria" };
Check(!PublicUrls.ShouldRedirectOrigin(request, settings), "Tailscale no redirige a otro dominio por defecto");
settings.RedirectToPublicOrigin = true;
Check(PublicUrls.ShouldRedirectOrigin(request, settings) && PublicUrls.Destination(request, settings, PublicUrls.ProductPath(ring), "") == "https://tienda.example/joyeria/producto/anillos-oro-de-18-quilates-02000090", "El dominio preferido solo redirige cuando se activa expresamente");
settings.IndexingEnabled = false;
Check(!PublicUrls.ShouldRedirectOrigin(request, settings), "No se fuerza el dominio en modo privado");
Check(PublicUrls.Canonical(settings, PublicUrls.ProductPath(ring)) == "https://tienda.example/joyeria/producto/anillos-oro-de-18-quilates-02000090", "La canónica no depende del Host ni añade la talla");
Check(PublicUrls.CatalogPath("anillos", 2) == "/catalogo/anillos?page=2" && PublicUrls.CatalogPath(null, 1) == "/catalogo", "Cada página del catálogo conserva su dirección y se omite page=1");

var allocated = CatalogUrlStore.Allocate(new[] {
    KeyValuePair.Create("pieza-a", "anillos-oro-02000090"), KeyValuePair.Create("pieza-b", "anillos-oro-02000090")
}, new Dictionary<string,string>());
Check(allocated.Values.Distinct().Count() == 2 && allocated["pieza-a"] == "anillos-oro-02000090" && allocated["pieza-b"] == "anillos-oro-02000090-alternativa-2", "Referencias iguales reciben direcciones distintas sin IDs de la base de datos");
var renamed = CatalogUrlStore.Allocate(new[] { KeyValuePair.Create("pieza-a", "otra-categoria-nueva") }, allocated);
Check(renamed["pieza-a"] == allocated["pieza-a"], "La dirección guardada no cambia al editar la categoría o el nombre");

foreach (var publicMode in new[] { false, true })
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
        ["Publication:IndexingEnabled"] = publicMode.ToString(), ["Publication:PublicBaseUrl"] = "https://tienda.example/joyeria" });
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.Services.AddPublicationFeatures(builder.Configuration);
    builder.Services.Configure<EmpresaSettings>(_ => { });
    builder.Services.AddSingleton(new CatalogoService(null!, null!, null!, null!));
    builder.Services.AddSingleton(new FeaturedProductsStore(builder.Configuration));
    builder.Services.AddSingleton<ICategoriaService>(Stub.Make<ICategoriaService>());
    builder.Services.AddSingleton<IUsuarioService>(Stub.Make<IUsuarioService>());
    builder.Services.AddSingleton<ICarritoService>(Stub.Make<ICarritoService>());
    builder.Services.AddSingleton<IJwtService>(Stub.Make<IJwtService>());
    builder.Services.AddSingleton<IPasswordResetTokenRepository>(Stub.Make<IPasswordResetTokenRepository>());
    builder.Services.AddSingleton<IEmailService>(Stub.Make<IEmailService>());
    builder.Services.AddSingleton<ITraficoService, TraficoService>();
    await using var app = builder.Build();
    app.UseRouting(); app.UsePublicationFeatures(); app.UseMiddleware<AldaJoyeros.Middleware.TraficoMiddleware>();
    app.MapPublicRoutes();
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); app.MapPublicationFallback();
    await app.StartAsync();
    try
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var jar = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = jar, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        var generator = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.LinkGenerator>();
        Check(generator.GetPathByAction("Detalle", "Productos", new { slug = "anillos-oro" }) == "/producto/anillos-oro", "Los enlaces generados usan la ruta de producto limpia");
        Check(generator.GetPathByAction("Index", "Productos") == "/catalogo", "La navegación genera catalogo");
        foreach (var pair in new Dictionary<string,string> {
            ["/Home"] = "/", ["/Home/Index?utm_source=correo"] = "/?utm_source=correo",
            ["/Productos"] = "/catalogo", ["/Productos/Index?categoriaId=3&page=2"] = "/catalogo?categoriaId=3&page=2",
            ["/Home/Privacy"] = "/privacidad", ["/Home/Cookies"] = "/cookies", ["/Home/AvisoLegal"] = "/aviso-legal",
            ["/Home/Terminos"] = "/condiciones-de-compra", ["/PRIVACIDAD/"] = "/privacidad" })
        {
            var response = await client.GetAsync(pair.Key);
            Check(response.StatusCode == HttpStatusCode.MovedPermanently && response.Headers.Location?.ToString() == pair.Value, "301 de " + pair.Key + " al destino esperado");
        }
        var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/Home/Cookies"));
        Check(head.StatusCode == HttpStatusCode.MovedPermanently && head.Headers.Location?.ToString() == "/cookies", "HEAD también redirige sin ejecutar la página antigua");
        var cookies = await client.GetAsync("/cookies"); var html = await cookies.Content.ReadAsStringAsync();
        Check(cookies.StatusCode == HttpStatusCode.OK && !jar.GetCookies(client.BaseAddress).Cast<Cookie>().Any(c => c.Name == VisitorConsent.VisitorCookie), "No se crea una cookie analítica sin permiso");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Check(token.Length > 0, "La elección de cookies incluye protección antifalsificación");
        async Task<HttpResponseMessage> Choose(bool analytics, string destination) => await client.PostAsync("/Consent/Save", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["analytics"] = analytics.ToString(), ["returnUrl"] = destination, ["__RequestVerificationToken"] = token }));
        var accepted = await Choose(true, "/privacidad");
        Check(accepted.StatusCode == HttpStatusCode.Redirect && accepted.Headers.Location!.ToString() == "/privacidad", "Aceptar conserva el destino local");
        await client.GetAsync("/privacidad");
        Check(jar.GetCookies(client.BaseAddress).Cast<Cookie>().Any(c => c.Name == VisitorConsent.VisitorCookie), "Solo tras aceptar se crea la cookie de visitas");
        var refused = await Choose(false, "https://evil.invalid/");
        Check(refused.Headers.Location!.ToString() == "/" && !jar.GetCookies(client.BaseAddress).Cast<Cookie>().Any(c => c.Name == VisitorConsent.VisitorCookie), "Retirar elimina la cookie y evita redirecciones externas");
        Check((await client.PostAsync("/Consent/Save", new FormUrlEncodedContent(new Dictionary<string,string> { ["analytics"] = "true" }))).StatusCode == HttpStatusCode.BadRequest, "Sin token no se altera el consentimiento");
        var missing = await client.GetAsync("/pagina-no-existente");
        Check(missing.StatusCode == HttpStatusCode.NotFound && (await missing.Content.ReadAsStringAsync()).Contains("No encontramos esta página"), "Una ruta inexistente muestra la 404 y conserva el estado HTTP");
        var robots = await client.GetStringAsync("/robots.txt");
        Check(publicMode ? robots.Contains("Sitemap: https://tienda.example/joyeria/sitemap.xml") : robots.Contains("Disallow: /"), "Robots respeta el modo de publicación");
        if (!publicMode) Check((await client.GetAsync("/sitemap.xml")).StatusCode == HttpStatusCode.NotFound, "El entorno privado no publica el sitemap");
        else
        {
            var sitemap = await client.GetStringAsync("/sitemap-0.xml");
            Check(sitemap.Contains("https://tienda.example/joyeria/aviso-legal") && !sitemap.Contains("/Auth"), "El sitemap público utiliza el dominio y no incluye acceso ni pedidos");
            Check(html.Contains("rel=\"canonical\" href=\"https://tienda.example/joyeria/cookies\""), "Canonical con el dominio configurado");
        }
        for (var i = 0; i < 10; i++)
            Check((await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "El login real rechaza formularios sin token");
        var limited = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string,string>()));
        Check((int)limited.StatusCode == 429 && limited.Headers.RetryAfter != null, "Los envíos excesivos al login real se limitan");
        if (publicMode)
        {
            app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PublicationOptions>>().Value.RedirectToPublicOrigin = true;
            var preferred = await client.GetAsync("/Home/Privacy?utm_source=correo");
            Check(preferred.StatusCode == HttpStatusCode.MovedPermanently && preferred.Headers.Location?.ToString() == "https://tienda.example/joyeria/privacidad?utm_source=correo", "El dominio preferido y la ruta antigua se unifican en una sola 301");
            Check((await client.GetAsync("/Auth/Login")).StatusCode == HttpStatusCode.OK, "La preferencia de dominio no altera el acceso ni su flujo de sesión");
        }
    }
    finally { await app.StopAsync(); }
}
Console.WriteLine($"{checks} comprobaciones superadas; sin bases de datos, correo ni pagos.");

public class Stub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new Exception("No se deben ejecutar servicios externos: " + method!.Name);
    public static T Make<T>() where T : class => Create<T, Stub>();
}
