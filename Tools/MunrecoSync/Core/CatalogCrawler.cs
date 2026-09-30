using Microsoft.Playwright;
using System.Text.Json;
using System.Text.Json.Nodes;

static class CatalogCrawler
{
    public static readonly (string Id,string Name,string Brand)[] Sections = [
        ("15","Viceroy Fashion","Viceroy"), ("16","Viceroy Jewels","Viceroy"),
        ("11","Viceroy Relojes","Viceroy"), ("13","Mark Maddox","Mark Maddox") ];
    public static async Task Select(IAPIRequestContext api,string id) {
        if(!Sections.Any(x=>x.Id==id)) throw new InvalidOperationException("Section not in observed accessible selectors");
        var form=api.CreateFormData(); form.Set("brand_selected",id);
        var res=await api.PostAsync("https://b2b.munreco.com/index.php?route=common/header/swap_manufacturer",
            new(){Form=form});
        if(!res.Ok || res.Url.Contains("account/login")) throw new InvalidOperationException("Section selection failed: "+res.Status);
        await res.DisposeAsync();
    }
    public static async Task Run(IPlaywright pw,string root) {
        await using var api=await pw.APIRequest.NewContextAsync(new(){StorageStatePath=Path.Combine(root,"auth.json"),Timeout=60000});
        var all=new Dictionary<string,JsonObject>();
        var coverage=new List<object>();
        foreach(var section in Sections) {
            await Select(api,section.Id);
            var dir=Path.Combine(root,"catalogs",section.Id); Directory.CreateDirectory(dir);
            var seen=new HashSet<string>();
            bool finished=false;
            for(int n=1;n<=3000;n++) {
                var path=Path.Combine(dir,$"{n:D4}.json");
                string body;
                if(File.Exists(path)) body=await File.ReadAllTextAsync(path);
                else {
                    body="";
                    for(int attempt=0;attempt<4;attempt++) {
                        var res=await api.GetAsync($"https://b2b.munreco.com/index.php?route=product/category&sort=p.model&order=ASC&page={n}&vue=1",
                            new(){Headers=new Dictionary<string,string>{{"X-Custom-Ajax","true"}}});
                        if(res.Status==429 || res.Status>=500){await res.DisposeAsync();await Task.Delay(10000*(attempt+1));continue;}
                        if(!res.Ok || res.Url.Contains("account/login")) throw new InvalidOperationException("Catalog authorization failure: "+res.Status);
                        body=await res.TextAsync();await res.DisposeAsync();break;
                    }
                    if(body.Length==0) throw new InvalidOperationException("Catalog retries exhausted");
                }
                var doc=JsonNode.Parse(body)!.AsObject();
                var products=doc["products"]!.AsArray();
                int added=0;
                foreach(var item in products) {
                    var p=item!.AsObject(); var id=p["product_id"]!.ToString();
                    if(seen.Add(id))added++;
                    if(!all.ContainsKey(id)) {
                        var clone=p.DeepClone().AsObject();
                        clone["_source_section"]=section.Id;clone["_source_section_name"]=section.Name;clone["_source_brand"]=section.Brand;
                        all.Add(id,clone);
                    }
                }
                await File.WriteAllTextAsync(path,body);
                var more=doc["more_pages"]?.ToString();
                bool hasMore=more=="1" || string.Equals(more,"true",StringComparison.OrdinalIgnoreCase);
                if(n%25==0 || !hasMore) Console.WriteLine($"SECTION {section.Name}: page={n} products={seen.Count} more={hasMore}");
                if(!hasMore) {coverage.Add(new{section.Id,section.Name,pages=n,products=seen.Count});finished=true;break;}
                if(added==0)throw new InvalidOperationException("Catalog page repeated before end");
                await Task.Delay(400);
            }
            if(!finished)throw new InvalidOperationException("Catalog limit reached");
            await File.WriteAllTextAsync(Path.Combine(root,"all-products-expanded.json"),JsonSerializer.Serialize(all.Values));
        }
        await File.WriteAllTextAsync(Path.Combine(root,"coverage.json"),JsonSerializer.Serialize(new{completedAt=DateTime.UtcNow,products=all.Count,sections=coverage,unavailable="Sandoz: selector no habilitado en la cuenta"},new JsonSerializerOptions{WriteIndented=true}));
        await api.StorageStateAsync(new(){Path=Path.Combine(root,"auth.json")});
        Console.WriteLine($"ALL_CATALOGS_COMPLETE products={all.Count}");
    }
}
