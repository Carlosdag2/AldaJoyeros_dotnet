using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

static class AdminUIWebChecks
{
    public static async Task Run(string root, IConfiguration config)
    {
        const string host = "http://127.0.0.1:5095";
        var output = Path.Combine(root, "App_Data", "AdminUI", "checks");
        Directory.CreateDirectory(output);
        string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = config["JwtSettings:Issuer"], aud = config["JwtSettings:Audience"], exp = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds(), nbf = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(), nameid = "1", userId = "1", email = "local-verification@example.invalid", role = "ADMIN", rol = "ADMIN" }));
        var key = config["JwtSettings:Secret"] ?? throw new Exception("JWT configuration missing");
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key));
        var token = header + "." + payload + "." + B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        await context.AddCookiesAsync(new[] { new Cookie { Name = "jwt_token", Value = token, Url = host } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        void Check(bool success, string message) { if (!success) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        async Task Ready() => await page.WaitForFunctionAsync("() => document.querySelector('[data-admin-results]')?.getAttribute('aria-busy') === 'false'");
        async Task Submit() { await page.Locator("[data-admin-search] button[type=submit]").ClickAsync(); await Ready(); }

        await page.GotoAsync(host + "/AdminCategorias");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-admin-status]')?.textContent.includes('encontradas')");
        Check(await page.Locator("[data-category-group]").CountAsync() > 0, "Categorías agrupadas");
        var count = await page.Locator("[data-categoria-id]:visible").CountAsync();
        await page.Locator("#buscarCategorias").FillAsync("Anillos");
        Check(await page.Locator("[data-categoria-id]:visible").CountAsync() > 0 && await page.Locator("[data-categoria-id]:visible").CountAsync() < count, "Buscador de categorías");
        await page.Locator("#buscarCategorias").FillAsync("sin-resultados-123456");
        Check(await page.Locator("[data-no-results]").IsVisibleAsync(), "Estado sin resultados de categorías");
        await page.Locator("[data-clear-search]").ClickAsync();
        Check(await page.Locator("[data-categoria-id]:visible").CountAsync() == count, "Limpiar filtros de categorías");
        Check(await page.Locator("[data-categoria-id]").Filter(new() { HasText = "Sin categoría" }).Locator("button").CountAsync() == 0, "Categoría del sistema protegida");
        var group = await page.Locator("select[name=grupo] option").Nth(1).GetAttributeAsync("value");
        await page.Locator("select[name=grupo]").SelectOptionAsync(group!);
        Check(await page.Locator("[data-category-group]:visible").CountAsync() == 1, "Filtro por grupo");
        await page.Locator("[data-clear-search]").ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, "categorias-desktop.png"), FullPage = true });

        foreach (var (route, record, filter) in new[] { ("Usuarios", "usuario", "ADMIN"), ("Pedidos", "pedido", "PENDIENTE") })
        {
            var response = await page.GotoAsync(host + "/Admin" + route);
            Check(response?.Status == 200, route + " accesible");
            await page.WaitForFunctionAsync("() => document.querySelector('[data-filter=\"\"]')?.getAttribute('aria-pressed') === 'true'");
            var rows = page.Locator("[data-" + record + "-id]");
            if (await rows.CountAsync() > 0)
            {
                var id = await rows.First.GetAttributeAsync("data-" + record + "-id");
                await page.Locator("input[name=busqueda]").FillAsync(id!);
                await Submit();
                Check(await rows.CountAsync() == 1, route + ": búsqueda por ID en todo el listado");
                await page.ReloadAsync();
                Check(await page.Locator("input[name=busqueda]").InputValueAsync() == id && await rows.CountAsync() == 1, route + ": búsqueda conservada al recargar");
                await page.Locator("[data-delete-record]").ClickAsync();
                await page.Locator(".btn-cancel").WaitForAsync();
                Check(await page.Locator(".btn-cancel").IsVisibleAsync(), route + ": confirmación antes de eliminar");
                await page.Locator(".btn-cancel").ClickAsync();
            }
            await page.Locator("input[name=busqueda]").FillAsync("sin-resultados-123456");
            await Submit();
            Check(await page.Locator(".admin-empty").IsVisibleAsync() && await page.Locator("#statTotal").InnerTextAsync() == "0", route + ": estado vacío y contador");
            await page.Locator("[data-clear-search]").ClickAsync(); await Ready();
            await page.Locator("[data-filter='" + filter + "']").ClickAsync(); await Ready();
            Check(await page.Locator("[data-filter='" + filter + "']").GetAttributeAsync("aria-pressed") == "true", route + ": filtro seleccionado");
            await page.Locator("[data-clear-search]").ClickAsync(); await Ready();
            if (await page.Locator("[data-scroll-pagination]").CountAsync() > 0)
            {
                await page.EvaluateAsync("() => window." + (route == "Usuarios" ? "adminUsuarios" : "adminPedidos") + ".cargarPagina(2)"); await Ready();
                Check(await page.Locator("[data-current-page]").GetAttributeAsync("data-current-page") == "2", route + ": paginado conservado");
            }
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, route.ToLowerInvariant() + "-desktop.png"), FullPage = true });
        }
        foreach (var width in new[] { 768, 1024, 1440 })
        {
            await page.SetViewportSizeAsync(width, 1000);
            foreach (var route in new[] { "Categorias", "Usuarios", "Pedidos" })
            {
                await page.GotoAsync(host + "/Admin" + route);
                Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), route + ": sin desbordamiento a " + width + "px");
                Check(await page.EvaluateAsync<bool>("getComputedStyle(document.querySelector('.admin-stats')).display === 'grid'"), route + ": rejilla de Tailwind aplicada");
                Check(await page.Locator("link[href*='admin-listas.css']").CountAsync() == 0, route + ": sin hoja responsive propia");
            }
        }
        await page.SetViewportSizeAsync(390, 844);
        foreach (var route in new[] { "Categorias", "Usuarios", "Pedidos" })
        {
            await page.GotoAsync(host + "/Admin" + route);
            await page.Locator(".admin-workspace").ScrollIntoViewIfNeededAsync();
            Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), route + ": móvil sin desbordamiento");
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, route.ToLowerInvariant() + "-mobile.png"), FullPage = true });
        }
        Check(errors.Count == 0, "Sin errores de JavaScript");
    }
}
