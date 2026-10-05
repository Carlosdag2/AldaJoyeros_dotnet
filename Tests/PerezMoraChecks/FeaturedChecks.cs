using AldaJoyeros.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class FeaturedChecks
{
    public static async Task Run(string project, IConfiguration config)
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var database = "alda_featured_test_" + Guid.NewGuid().ToString("N");
        await using var sql = new MySqlConnection(config.GetConnectionString("DefaultConnection"));
        await sql.OpenAsync();
        async Task Execute(string query) { await using var cmd = new MySqlCommand(query, sql); await cmd.ExecuteNonQueryAsync(); }
        await Execute($"CREATE DATABASE `{database}`");
        try
        {
            await Execute($"CREATE TABLE `{database}`.producto (id bigint PRIMARY KEY, eliminado tinyint NOT NULL)");
            await Execute($"INSERT INTO `{database}`.producto VALUES (1,0),(2,0),(3,0),(4,0),(5,0),(6,0),(7,0),(8,0),(9,0),(10,0),(11,1)");
            var testConnection = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")!) { Database = database, Pooling = false };
            var testConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = testConnection.ConnectionString }).Build();
            var store = new FeaturedProductsStore(testConfig);
            Check((await store.GetIdsAsync()).SequenceEqual(Enumerable.Range(1, 8).Select(x => (long)x)), "Conserva los ocho destacados iniciales");
            await store.ChangeAsync(1, "añadir");
            Check((await store.GetIdsAsync()).Count == 8, "No duplica productos");
            async Task Reject(long id, string action, string label)
            {
                try { await store.ChangeAsync(id, action); } catch (ArgumentException) { Check(true, label); return; }
                throw new Exception(label);
            }
            await Reject(9, "añadir", "Valida el máximo de ocho en el servidor");
            await store.ChangeAsync(1, "quitar");
            await Reject(11, "añadir", "Rechaza productos ocultos");
            await Reject(999, "añadir", "Rechaza productos inexistentes");
            await store.ChangeAsync(9, "añadir");
            await store.ChangeAsync(9, "subir");
            Check((await store.GetIdsAsync())[^2] == 9, "Permite ordenar destacados");
            await store.ChangeAsync(9, "bajar");
            Check((await store.GetIdsAsync())[^1] == 9, "Permite bajar destacados");
            Check((await new FeaturedProductsStore(testConfig).GetIdsAsync()).SequenceEqual(await store.GetIdsAsync()), "Selección persistente tras reiniciar el servicio");
            await store.ChangeAsync(9, "quitar");
            async Task<bool> Add(long id) { try { await store.ChangeAsync(id, "añadir"); return true; } catch (ArgumentException) { return false; } }
            var concurrent = await Task.WhenAll(Add(9), Add(10));
            Check(concurrent.Count(x => x) == 1 && (await store.GetIdsAsync()).Count == 8, "Actualizaciones simultáneas sin perder cambios ni superar el límite");
            foreach (var id in await store.GetIdsAsync()) await store.ChangeAsync(id, "quitar");
            Check((await new FeaturedProductsStore(testConfig).GetIdsAsync()).Count == 0, "Selección vacía no se rellena automáticamente");
        }
        finally { await Execute($"DROP DATABASE `{database}`"); }

        const string host = "http://127.0.0.1:5096";
        string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string Token(string role)
        {
            var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = config["JwtSettings:Issuer"], aud = config["JwtSettings:Audience"], exp = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds(), nameid = "1", userId = "1", email = "local-verification@example.invalid", role, rol = role }));
            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(config["JwtSettings:Secret"]!));
            return header + "." + payload + "." + B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        }
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(host + "/AdminDestacados");
        Check(page.Url.Contains("/Auth"), "Destacados exige iniciar sesión");
        await context.AddCookiesAsync(new[] { new Cookie { Name = "jwt_token", Value = Token("USER"), Url = host } });
        Check((await context.APIRequest.GetAsync(host + "/AdminDestacados")).Status == 403, "Solo el administrador puede gestionar destacados");
        await context.AddCookiesAsync(new[] { new Cookie { Name = "jwt_token", Value = Token("ADMIN"), Url = host } });
        Check((await page.GotoAsync(host + "/AdminDestacados"))?.Status == 200, "Selector accesible para administrador");
        var selected = await page.Locator("[data-featured-id]").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.featuredId)");
        Check(selected.Length <= 8 && selected.Distinct().Count() == selected.Length, "Selección sin duplicados en el panel");
        Check(await page.Locator("form[action*='Cambiar'] input[name=__RequestVerificationToken]").CountAsync() > 0, "Formularios protegidos contra CSRF");
        var request = await context.APIRequest.PostAsync(host + "/AdminDestacados/Cambiar", new() { Data = "id=1&accion=quitar", Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" } });
        Check(request.Status == 400, "Servidor rechaza cambios sin token CSRF");
        var output = Path.Combine(project, "App_Data", "Featured", "checks"); Directory.CreateDirectory(output);
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, "desktop.png"), FullPage = true });
        await page.Locator("#buscarDestacados").FillAsync("sin-resultados-123456");
        await page.GetByRole(AriaRole.Button, new() { Name = "Buscar", Exact = true }).ClickAsync();
        Check(await page.Locator("[data-candidate-id]").CountAsync() == 0, "Buscador del catálogo");
        Check((await page.Locator("[data-featured-id]").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.featuredId)")).SequenceEqual(selected), "La búsqueda conserva la selección");
        await page.GotoAsync(host + "/");
        var links = await page.Locator("#productos-destacados a[href*='/Productos/Detalle/']").EvaluateAllAsync<string[]>("links => links.map(x => x.getAttribute('href').split('/').pop())");
        Check(links.Distinct().SequenceEqual(selected), "El inicio respeta los destacados y su orden");
        await page.SetViewportSizeAsync(390, 844); await page.GotoAsync(host + "/AdminDestacados");
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Selector responsive sin desbordamiento");
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, "mobile.png"), FullPage = true });
        Check(errors.Count == 0, "Sin errores de JavaScript");
    }
}
