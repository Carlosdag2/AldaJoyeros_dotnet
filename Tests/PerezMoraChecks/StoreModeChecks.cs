using AldaJoyeros.Services;
using Microsoft.Playwright;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class StoreModeChecks
{
    public static async Task Run(string project, IConfiguration config)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS: " + message);
        }

        var database = "alda_mode_test_" + Guid.NewGuid().ToString("N");
        await using var sql = new MySqlConnection(config.GetConnectionString("DefaultConnection"));
        await sql.OpenAsync();
        async Task Execute(string query)
        {
            await using var command = new MySqlCommand(query, sql);
            await command.ExecuteNonQueryAsync();
        }
        await Execute($"CREATE DATABASE `{database}`");
        try
        {
            var connection = new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")!) { Database = database, Pooling = false };
            var testConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            var isolated = new StoreModeStore(testConfig);
            Check(!await isolated.EnabledAsync(), "Una instalación nueva empieza en escaparate");
            await isolated.SetAsync(true);
            Check(await new StoreModeStore(testConfig).EnabledAsync(), "La tienda activa persiste tras reiniciar");
            await isolated.SetAsync(false);
            Check(!await isolated.EnabledAsync(), "Puede volver a escaparate");
        }
        finally { await Execute($"DROP DATABASE `{database}`"); }

        const string host = "http://127.0.0.1:5098";
        string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string Token(string role)
        {
            var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = config["JwtSettings:Issuer"], aud = config["JwtSettings:Audience"], exp = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds(), nameid = "1", userId = "1", email = "local-verification@example.invalid", role, rol = role }));
            using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(config["JwtSettings:Secret"]!));
            return header + "." + payload + "." + B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        }
        var store = new StoreModeStore(config);
        var original = await store.EnabledAsync();
        try
        {
            await store.SetAsync(false);
            using var pw = await Playwright.CreateAsync();
            await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
            var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
            var page = await context.NewPageAsync();
            var errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);
            await page.GotoAsync(host + "/");
            Check(await page.Locator("a[href*='/Carrito'], button[onclick*='agregarAlCarrito'], button[\\@click*='agregarAlCarrito']").CountAsync() == 0, "Inicio sin carrito ni botones de compra");
            Check((await context.APIRequest.PostAsync(host + "/Carrito/Agregar", new() { Data = "productoId=0&cantidad=1", Headers = new Dictionary<string,string> { ["Content-Type"] = "application/x-www-form-urlencoded" } })).Status == 403, "Escaparate impide crear carritos temporales sin sesión");
            await context.AddCookiesAsync(new[] { new Cookie { Name = "jwt_token", Value = Token("USER"), Url = host } });
            Check((await context.APIRequest.PostAsync(host + "/AdminTienda/Cambiar", new() { Data = "ecommerce=true" })).Status == 403, "Un cliente no puede cambiar el modo");
            await context.AddCookiesAsync(new[] { new Cookie { Name = "jwt_token", Value = Token("ADMIN"), Url = host } });
            await page.GotoAsync(host + "/Admin");
            Check(await page.GetByText("Escaparate activo", new() { Exact = true }).CountAsync() == 1, "Panel muestra el modo actual");
            var form = page.Locator("form[action='/AdminTienda/Cambiar']");
            var csrf = await form.Locator("input[name=__RequestVerificationToken]").InputValueAsync();
            var headers = new Dictionary<string,string> { ["Content-Type"] = "application/x-www-form-urlencoded" };
            Check((await context.APIRequest.PostAsync(host + "/AdminTienda/Cambiar", new() { Data = "ecommerce=true", Headers = headers })).Status == 400, "Cambio de modo protegido contra CSRF");
            foreach (var action in new[] { "Agregar", "AgregarAjax", "Actualizar", "Eliminar", "Vaciar", "ActualizarAjax", "EliminarAjax", "VaciarAjax" })
            {
                var response = await context.APIRequest.PostAsync(host + "/Carrito/" + action, new() { Data = "productoId=0&itemId=0&cantidad=1&__RequestVerificationToken=" + Uri.EscapeDataString(csrf), Headers = headers });
                Check(response.Status == 403, "Escaparate bloquea Carrito/" + action);
            }
            foreach (var action in new[] { "GuardarDireccion", "CreatePaymentIntent", "ConfirmarPago", "FinalizarPedido" })
            {
                var response = await context.APIRequest.PostAsync(host + "/Pedidos/" + action, new() { Data = "__RequestVerificationToken=" + Uri.EscapeDataString(csrf), Headers = headers });
                Check(response.Status == 403, "Escaparate bloquea Pedidos/" + action);
            }
            Check((await context.APIRequest.PostAsync(host + "/carrito/agregarajax", new() { Data = "productoId=0&cantidad=1", Headers = headers })).Status == 403, "No se elude el bloqueo cambiando mayúsculas de la URL");
            Check((await context.APIRequest.GetAsync(host + "/Carrito/GetResumenAjax")).Status == 403, "El resumen AJAX del carrito también está desactivado");
            foreach (var path in new[] { "/Carrito", "/Pedidos/Checkout", "/Pedidos/CheckoutPago", "/Pedidos/CheckoutConfirmacion" })
            {
                await page.GotoAsync(host + path);
                Check(new Uri(page.Url).AbsolutePath == "/Productos", "Acceso directo redirigido: " + path);
            }
            var detail = await page.Locator("a[href*='/Productos/Detalle/']").First.GetAttributeAsync("href");
            Check(detail != null, "Catálogo disponible");
            await page.GotoAsync(host + detail);
            Check(await page.GetByRole(AriaRole.Link, new() { Name = "Consultar en tienda" }).CountAsync() == 1, "Detalle ofrece contacto en escaparate");
            Check(await page.Locator("a[href*='/Carrito'], button[\\@click*='agregarAlCarrito']").CountAsync() == 0, "Detalle sin compras");
            var partial = await context.APIRequest.GetAsync(host + "/Productos/Filtrar?page=2");
            using var json = JsonDocument.Parse(await partial.TextAsync());
            Check(!json.RootElement.GetProperty("html").GetString()!.Contains("agregarAlCarrito"), "Los filtros AJAX respetan escaparate");
            var output = Path.Combine(project, "App_Data", "StoreMode", "checks"); Directory.CreateDirectory(output);
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, "showcase-detail.png"), FullPage = true });
            await page.GotoAsync(host + "/Admin");
            await page.GetByRole(AriaRole.Button, new() { Name = "Activar tienda online", Exact = true }).ClickAsync();
            await page.WaitForURLAsync(host + "/Admin");
            Check(await store.EnabledAsync(), "El botón del administrador activa ecommerce");
            await page.GotoAsync(host + detail);
            Check(await page.GetByRole(AriaRole.Button, new() { Name = "Añadir al Carrito" }).CountAsync() == 1, "Ecommerce recupera la compra del producto");
            Check(await page.Locator("a[href='/Carrito']").CountAsync() > 0, "Ecommerce recupera enlaces al carrito");
            using (var ecommercePartial = JsonDocument.Parse(await (await context.APIRequest.GetAsync(host + "/Productos/Filtrar?page=2")).TextAsync()))
                Check(ecommercePartial.RootElement.GetProperty("html").GetString()!.Contains("agregarAlCarrito"), "Ecommerce recupera botones en filtros AJAX");
            await page.GotoAsync(host + "/Admin");
            await page.GetByRole(AriaRole.Button, new() { Name = "Usar escaparate", Exact = true }).ClickAsync();
            await page.WaitForURLAsync(host + "/Admin");
            Check(!await store.EnabledAsync(), "El botón del administrador desactiva ecommerce");
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, "admin-desktop.png"), FullPage = true });
            await page.SetViewportSizeAsync(390, 844);
            Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Panel responsive sin desbordamiento");
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, "admin-mobile.png"), FullPage = true });
            Check(errors.Count == 0, "Sin errores JavaScript");
        }
        finally { await store.SetAsync(original); }
    }
}
