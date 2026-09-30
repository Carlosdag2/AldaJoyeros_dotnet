using Microsoft.Playwright;
using System.Text.Json;
using AngleSharp.Html.Parser;

static class ProviderLogin {
    public static async Task Run() {
        using var input=JsonDocument.Parse(await Console.In.ReadToEndAsync());
        using var pw=await Playwright.CreateAsync();
        await using var api=await pw.APIRequest.NewContextAsync(new(){Timeout=30000});
        const string url="https://b2b.munreco.com/index.php?route=account/login";
        var page=await api.GetAsync(url);var html=await page.TextAsync();await page.DisposeAsync();
        var doc=await new HtmlParser().ParseDocumentAsync(html);
        var component=doc.QuerySelector("kri-login-form")??throw new InvalidOperationException("Formulario no reconocido");
        if(component.HasAttribute("login-company"))throw new InvalidOperationException("El acceso requiere seleccionar empresa");
        var form=api.CreateFormData();
        form.Set(component.GetAttribute("user-field")??"username",input.RootElement.GetProperty("username").GetString()!);
        form.Set(component.GetAttribute("password-field")??"password",input.RootElement.GetProperty("password").GetString()!);
        var csrf=component.GetAttribute("csrf");if(!string.IsNullOrEmpty(csrf))form.Set(component.GetAttribute("csrf-name")??"_csrf",csrf);
        var response=await api.PostAsync(url,new(){Form=form});await response.DisposeAsync();
        await CatalogCrawler.Select(api,"15");
        var check=await api.GetAsync("https://b2b.munreco.com/index.php?route=product/category&sort=p.model&order=ASC&page=1&vue=1",new(){Headers=new Dictionary<string,string>{{"X-Custom-Ajax","true"}}});
        try {
            if(!check.Ok||check.Url.Contains("account/login"))throw new InvalidOperationException("Acceso no confirmado");
            using var catalog=JsonDocument.Parse(await check.TextAsync());
            if(catalog.RootElement.GetProperty("products").GetArrayLength()==0)throw new InvalidOperationException("Catalogo no disponible");
        } finally {await check.DisposeAsync();}
        Console.Write(await api.StorageStateAsync());
    }
}
