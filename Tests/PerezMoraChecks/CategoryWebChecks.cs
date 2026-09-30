using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using MySqlConnector;
using System.Text.Json;

static class CategoryWebChecks
{
    public static async Task Run(string root,IConfiguration config)
    {
        var output=Path.Combine(root,"App_Data","CategoryOrganization","checks");Directory.CreateDirectory(output);
        await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();
        var expected=new Dictionary<string,int>();
        await using(var cmd=new MySqlCommand("SELECT c.nombre,COUNT(p.id) FROM categoria c JOIN producto p ON p.categoria_id=c.id AND p.eliminado=0 GROUP BY c.id,c.nombre",sql))
        {await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())expected[reader.GetString(0)]=reader.GetInt32(1);}
        using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Channel="msedge",Headless=true});
        var page=await browser.NewPageAsync(new(){ViewportSize=new(){Width=1440,Height=1000}});
        var errors=new List<string>();page.PageError+=(_,error)=>errors.Add(error);
        void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        var response=await page.GotoAsync("http://127.0.0.1:5092/Productos");Check(response?.Status==200,"Catálogo accesible");
        await page.WaitForFunctionAsync("() => !!window.Alpine && !!document.querySelector('#listaCategorias')");
        Check(await page.Locator("#listaCategorias details").CountAsync()==4,"Cuatro grupos de categorías");
        Check(await page.Locator("#listaCategorias .categoria-link").CountAsync()==expected.Count+1,"Solo categorías con productos visibles");
        Check(!await page.Locator("#listaCategorias").InnerTextAsync().ContinueWith(t=>t.Result.Contains("revisar")),"Productos pendientes fuera de los filtros públicos");
        async Task Filter(ILocator button,string name)
        {
            var res=await page.RunAndWaitForResponseAsync(async()=>await button.ClickAsync(),r=>r.Url.Contains("/Productos/Filtrar"));
            using var json=JsonDocument.Parse(await res.TextAsync());Check(json.RootElement.GetProperty("totalItems").GetInt32()==expected[name],"Filtro "+name+" coincide con MySQL");
            await page.WaitForFunctionAsync("() => !document.querySelector('[x-show=\"cargando\"]') || getComputedStyle(document.querySelector('[x-show=\"cargando\"]')).display === 'none'");
        }
        await Filter(page.Locator("#listaCategorias button").Filter(new(){HasText="Anillos"}),"Anillos");
        await page.Locator("#listaCategorias summary").Filter(new(){HasText="Relojería"}).ClickAsync();
        await Filter(page.Locator("#listaCategorias button").Filter(new(){HasText="Relojes ("}),"Relojes");
        await page.ScreenshotAsync(new(){Path=Path.Combine(output,"categorias-desktop.png"),FullPage=true});
        await page.SetViewportSizeAsync(390,844);await page.GotoAsync("http://127.0.0.1:5092/Productos");
        await page.GetByText("Filtrar por categoría",new(){Exact=true}).ClickAsync();
        await Filter(page.Locator("button").Filter(new(){HasText="Pulseras"}).Last,"Pulseras");
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"),"Móvil sin desbordamiento horizontal");
        await page.ScreenshotAsync(new(){Path=Path.Combine(output,"categorias-mobile.png"),FullPage=true});
        Check(errors.Count==0,"Sin errores de JavaScript");
        Console.WriteLine("Comprobaciones de categorías completadas.");
    }
}
