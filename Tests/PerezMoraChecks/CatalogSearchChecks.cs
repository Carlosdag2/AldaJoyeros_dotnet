using AldaJoyeros.Helpers;
using Microsoft.Playwright;
using System.Text.Json;

static class CatalogSearchChecks
{
    public static async Task Run(string project)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        int Ring(string query, string description = "Sortija de oro de 18 quilates", string category = "Anillos", string name = "PM-900") => new CatalogSearch(query).Score(name, description, category, new[] { "PM-900" }, Array.Empty<string>());
        foreach (var query in new[] { "anillo 18k", "anillos 18 quilates", "SORTIJA 18KT", "anillo de 18 kilates", "18 k anillo", "anil 18k", "anilo 18k" })
            Check(Ring(query) > 0, "Reconoce variante: " + query);
        foreach (var description in new[] { "Anillo de oro 18k", "Anillo de oro 18 kt", "Anillo de oro 18 quilates", "Anillo de oro ley 750", "Anillo de oro 750 milésimas", "Anillo de oro 750‰" })
            Check(Ring("anillo 18k", description) > 0, "Equivalencia de unidades: " + description);
        Check(Ring("anillo 18k", "Oro de 14 quilates") == 0, "No confunde 14 y 18 quilates");
        Check(Ring("anillo 18k", "Plata 925 referencia 750") == 0, "No convierte referencias numéricas en quilates");
        Check(Ring("anillo 18k", "Collar de oro 18 quilates", "Collares") == 0, "Exige que coincidan todos los conceptos");
        Check(Ring("anillo 18k", "Pendiente de anilla de oro 18k", "Pendientes") == 0, "No confunde anillos con anillas");
        Check(Ring("anillo 18k", "Anillo con diamante de 18 ct") == 0, "No confunde peso de diamantes con ley del oro");
        Check(Ring("anillo 18k", "Alianza de oro 18 quilates", "Alianzas") > 0, "Incluye alianzas al buscar anillos");
        Check(Ring("alianza 18k") == 0, "La búsqueda específica de alianzas no devuelve cualquier anillo");
        Check(Ring("pm900") > 0 && Ring("PM-900") > 0, "Referencias con y sin separadores");
        Check(Ring("PM-900") > Ring("anillo 18k"), "Referencia exacta con máxima prioridad");
        Check(new CatalogSearch("pendiente plata").Score("Aretes", "Plata de ley", "Pendientes", Array.Empty<string>(), Array.Empty<string>()) > 0, "Sinónimos de pendientes");
        Check(new CatalogSearch("anillo 18k").Score("Referencia 123", "", "Anillos", Array.Empty<string>(), CatalogSearch.AttributeText("{\"attribute\":[{\"name\":\"Material\",\"text\":\"Oro 18 kilates\"}]}")) > 0, "Busca en características del proveedor");
        Check(Ring("reloj") == 0 && Ring("de la") == 0, "No devuelve resultados para términos ajenos o vacíos");
        Check(Ring("anillo 18k", "Oro de 18 quilates", "Anillos", "anillo 18k") > Ring("anillo 18k"), "Nombre exacto por delante de coincidencia semántica");

        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        const string host = "http://127.0.0.1:5097";
        async Task<(int Count, long[] Ids)> Results(string query)
        {
            var response = await context.APIRequest.GetAsync(host + "/Productos/Filtrar?busqueda=" + Uri.EscapeDataString(query));
            Check(response.Status == 200, "Catálogo responde a " + query);
            using var json = JsonDocument.Parse(await response.TextAsync());
            var html = json.RootElement.GetProperty("html").GetString()!;
            var ids = System.Text.RegularExpressions.Regex.Matches(html, @"/Productos/Detalle/(\d+)").Select(m => long.Parse(m.Groups[1].Value)).Distinct().ToArray();
            return (json.RootElement.GetProperty("totalItems").GetInt32(), ids);
        }
        var baseline = await Results("anillo 18k");
        Check(baseline.Count > 0, "Anillo 18k encuentra productos del catálogo real");
        foreach (var query in new[] { "anillos 18 quilates", "sortija 18 kilates", "18kt anillo" })
        {
            var equivalent = await Results(query);
            Check(equivalent.Count == baseline.Count && equivalent.Ids.SequenceEqual(baseline.Ids), "Mismos resultados y orden para " + query);
        }
        var suggestions = await context.APIRequest.GetAsync(host + "/Productos/Buscar?q=anillo%2018k");
        using var suggestionJson = JsonDocument.Parse(await suggestions.TextAsync());
        var suggestionIds = suggestionJson.RootElement.GetProperty("sugerencias").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
        Check(suggestionIds.SequenceEqual(baseline.Ids.Take(5)), "Sugerencias y catálogo usan la misma relevancia");
        var page = await context.NewPageAsync();
        var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(host + "/Productos");
        var input = page.Locator("input[x-model=consulta]");
        await input.FillAsync("anillo 18k");
        await page.WaitForFunctionAsync("() => [...document.querySelectorAll('[x-show]')].some(x => x.getAttribute('x-show')?.includes('mostrarSugerencias') && getComputedStyle(x).display !== 'none')");
        Check(await page.Locator("a[href*='/Productos/Detalle/']").CountAsync() > 0, "Sugerencias predictivas visibles al escribir");
        var output = Path.Combine(project, "App_Data", "CatalogSearch", "checks"); Directory.CreateDirectory(output);
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, "desktop.png"), FullPage = true });
        await page.GotoAsync(host + "/Productos?busqueda=" + Uri.EscapeDataString("anillo de 18 quilates"));
        await page.SetViewportSizeAsync(390, 844);
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Buscador responsive sin desbordamiento");
        Check(errors.Count == 0, "Sin errores JavaScript al buscar");
        Console.WriteLine("Resultados reales para anillo 18k: " + baseline.Count);
    }
}
