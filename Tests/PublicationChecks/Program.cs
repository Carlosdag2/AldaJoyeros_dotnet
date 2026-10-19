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
    builder.Services.AddSingleton(new CatalogoService(null!, null!, null!));
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
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); app.MapPublicationFallback();
    await app.StartAsync();
    try
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var jar = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = jar, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        var cookies = await client.GetAsync("/Home/Cookies"); var html = await cookies.Content.ReadAsStringAsync();
        Check(cookies.StatusCode == HttpStatusCode.OK && !jar.GetCookies(client.BaseAddress).Cast<Cookie>().Any(c => c.Name == VisitorConsent.VisitorCookie), "No se crea una cookie analítica sin permiso");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Check(token.Length > 0, "La elección de cookies incluye protección antifalsificación");
        async Task<HttpResponseMessage> Choose(bool analytics, string destination) => await client.PostAsync("/Consent/Save", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["analytics"] = analytics.ToString(), ["returnUrl"] = destination, ["__RequestVerificationToken"] = token }));
        var accepted = await Choose(true, "/Home/Privacy");
        Check(accepted.StatusCode == HttpStatusCode.Redirect && accepted.Headers.Location!.ToString() == "/Home/Privacy", "Aceptar conserva el destino local");
        await client.GetAsync("/Home/Privacy");
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
            Check(sitemap.Contains("https://tienda.example/joyeria/Home/AvisoLegal") && !sitemap.Contains("/Auth"), "El sitemap público utiliza el dominio y no incluye acceso ni pedidos");
            Check(html.Contains("rel=\"canonical\" href=\"https://tienda.example/joyeria/Home/Cookies\""), "Canonical con el dominio configurado");
        }
        for (var i = 0; i < 10; i++)
            Check((await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode == HttpStatusCode.BadRequest, "El login real rechaza formularios sin token");
        var limited = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string,string>()));
        Check((int)limited.StatusCode == 429 && limited.Headers.RetryAfter != null, "Los envíos excesivos al login real se limitan");
    }
    finally { await app.StopAsync(); }
}
Console.WriteLine($"{checks} comprobaciones superadas; sin bases de datos, correo ni pagos.");

public class Stub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new Exception("No se deben ejecutar servicios externos: " + method!.Name);
    public static T Make<T>() where T : class => Create<T, Stub>();
}
