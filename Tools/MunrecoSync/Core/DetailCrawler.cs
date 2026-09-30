using Microsoft.Playwright;
using AngleSharp.Html.Parser;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Globalization;
using System.Collections.Concurrent;

static class DetailCrawler
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web){WriteIndented=true};
    static readonly SemaphoreSlim Rate = new(1,1);
    static DateTime NextRequest = DateTime.MinValue;
    static async Task Pace() {
        await Rate.WaitAsync();
        try { var delay=NextRequest-DateTime.UtcNow;if(delay>TimeSpan.Zero)await Task.Delay(delay);NextRequest=DateTime.UtcNow.AddMilliseconds(350); }
        finally {Rate.Release();}
    }
    static string Text(string? value)=>Regex.Replace(value??"",@"\s+"," ").Trim();
    static async Task<IAPIResponse> Get(IAPIRequestContext api,string url) {
        var uri=new Uri(url);
        if(uri.Scheme!="https" || uri.Host!="b2b.munreco.com")throw new InvalidOperationException("Unexpected source host");
        for(int i=0;i<4;i++) {
            await Pace();
            IAPIResponse r;
            try {r=await api.GetAsync(url,new(){Timeout=60000});}
            catch(PlaywrightException) when(i<3) {await Task.Delay(3000*(i+1));continue;}
            if(r.Status==429 || r.Status>=500) {await r.DisposeAsync();await Task.Delay(10000*(i+1));continue;}
            if(!r.Ok || r.Url.Contains("account/login")) {var status=r.Status;await r.DisposeAsync();throw new InvalidOperationException("Source response "+status);}
            return r;
        }
        throw new InvalidOperationException("Request retries exhausted");
    }
    public static async Task Run(IPlaywright pw,string root,bool sample,string outputDirectory,string? imageStore=null) {
        var output=Path.GetFullPath(outputDirectory);
        var store=Path.GetFullPath(imageStore??output);
        var records=Path.Combine(output,"productos");var images=Path.Combine(store,"imagenes");
        var cache=Path.Combine(root,"image-cache");
        foreach(var d in new[]{records,images,cache})Directory.CreateDirectory(d);
        var all=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root,"all-products-expanded.json")))!.AsArray();
        var errors=new ConcurrentBag<object>();int done=0,skipped=0;
        await using var api=await pw.APIRequest.NewContextAsync(new(){StorageStatePath=Path.Combine(root,"auth.json"),Timeout=60000});
        foreach(var section in CatalogCrawler.Sections) {
            var batch=all.Where(x=>x!["_source_section"]!.ToString()==section.Id).Select(x=>x!.AsObject()).ToArray();
            if(sample)batch=batch.Take(2).ToArray();
            await CatalogCrawler.Select(api,section.Id);
            await Parallel.ForEachAsync(batch,new ParallelOptions{MaxDegreeOfParallelism=3},async (raw,ct)=> {
                var id=raw["product_id"]!.ToString();var reference=raw["model"]!.ToString();
                var recordPath=Path.Combine(records,id+".json");
                try {
                    if(File.Exists(recordPath)) {Interlocked.Increment(ref skipped);return;}
                    var url="https://b2b.munreco.com/index.php?route=product/product&product_id="+Uri.EscapeDataString(id);
                    var res=await Get(api,url);string html;
                    try {html=await res.TextAsync();}finally{await res.DisposeAsync();}
                    var doc=await new HtmlParser().ParseDocumentAsync(html,ct);
                    if(doc.QuerySelector("input[type=password]")!=null)throw new InvalidOperationException("Session expired");
                    if(doc.QuerySelector("input[name=product_id]")?.GetAttribute("value")!=id)throw new InvalidOperationException("Detail product ID mismatch");
                    var refText=Text(doc.QuerySelector(".collection + h2")?.TextContent);
                    var structured=doc.QuerySelectorAll("script[type='application/ld+json']")
                        .Select(x=>JsonNode.Parse(x.TextContent)).FirstOrDefault(x=>x?["@type"]?.ToString()=="Product")
                        ?? throw new InvalidOperationException("Product structured data missing");
                    if(structured["mpn"]?.ToString()!=reference)throw new InvalidOperationException("Detail reference mismatch");
                    var currency=structured["offers"]?["priceCurrency"]?.ToString();
                    if(currency!="EUR")throw new InvalidOperationException("Unexpected currency");
                    if(!decimal.TryParse(structured["offers"]?["price"]?.ToString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var price)||price<=0)throw new InvalidOperationException("Invalid PVP");
                    if(!Text(doc.QuerySelector(".pvp_price")?.TextContent).Equals("PVP",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Price is not identified as PVP");
                    var descriptions=doc.QuerySelectorAll("#content .description").Select(x=>Text(x.TextContent)).Where(x=>x.Length>0).Distinct().ToArray();
                    // Preserve incomplete source records without inventing their missing details.
                    var shortDescription=descriptions.Length>0?descriptions[^1]:Text(raw["name"]?.ToString());
                    var attrs=new List<object>();
                    foreach(var row in doc.QuerySelectorAll(".characteristics p")) {
                        var value=Text(row.QuerySelector("span")?.TextContent);
                        var name=Text(string.Concat(row.ChildNodes.Where(x=>x.NodeType==AngleSharp.Dom.NodeType.Text).Select(x=>x.TextContent))).TrimEnd(':').Trim();
                        if(name.Length>0 && value.Length>0)attrs.Add(new {name,value});
                    }
                    var gallery=doc.QuerySelectorAll(".carousel-images [data-zoom]")
                        .Select(x=>new{Original=x.GetAttribute("data-zoom")!,Fallback=x.QuerySelector("img")?.GetAttribute("src")})
                        .Where(x=>!string.IsNullOrWhiteSpace(x.Original)).DistinctBy(x=>x.Original).ToArray();
                    if(gallery.Length==0)throw new InvalidOperationException("Product gallery missing");
                    var imageRecords=new List<JsonObject>();var imageWarnings=new List<object>();var hashes=new HashSet<string>();
                    foreach(var photo in gallery) {
                        if(new Uri(photo.Original).AbsolutePath.EndsWith("/placeholder.png",StringComparison.OrdinalIgnoreCase)) {
                            imageWarnings.Add(new{url=photo.Original,reason="El proveedor solo publica una imagen generica de sustitucion"});
                            continue;
                        }
                        JsonObject im;
                        try {
                            try {im=await Download(api,photo.Original,photo.Original,store,cache);}
                            catch(InvalidOperationException) when(!string.IsNullOrEmpty(photo.Fallback)) {im=await Download(api,photo.Fallback!,photo.Original,store,cache);im["fallback"]=true;}
                        } catch(InvalidOperationException e) when(e.Message=="Empty source image") {
                            imageWarnings.Add(new{url=photo.Original,reason="El proveedor devuelve un archivo vacio; no existe fotografia utilizable"});
                            continue;
                        }
                        if(hashes.Add(im["sha256"]!.ToString()))imageRecords.Add(im);
                    }
                    var requiresReview=descriptions.Length==0 || imageRecords.Count==0;
                    var record=new {
                        source="Munreco",sourceId=id,reference,brand=section.Brand,section=section.Name,sectionId=section.Id,
                        collection=raw["name"]?.ToString(),title=shortDescription,
                        description=string.Join("\n\n",descriptions),descriptionParts=descriptions,
                        price,currency,priceType="PVP",sourceUrl=url,
                        supplierQuantity=raw["quantity"]?.ToString(),supplierAvailability=raw["availability"]?.ToString(),
                        supplierStockStatus=raw["stock_status"]?.ToString(),attributes=attrs,
                        attributeGroups=raw["attribute_groups"]?.DeepClone(),
                        images=imageRecords,imageWarnings,requiresReview,sourceData=raw.DeepClone(),capturedAt=DateTime.UtcNow
                    };
                    await Atomic(recordPath,JsonSerializer.Serialize(record,Json));
                    var count=Interlocked.Increment(ref done);
                    if(sample || count%25==0)Console.WriteLine($"DETAILS done={count} cached={skipped} last={reference} images={imageRecords.Count}");
                } catch(Exception e) {
                    errors.Add(new {sourceId=id,reference,error=e.GetType().Name,message=e is InvalidOperationException?e.Message:"Download or parse failed"});
                    Console.WriteLine($"DETAIL_ERROR {id} {reference}: {e.GetType().Name}");
                }
            });
        }
        await api.StorageStateAsync(new(){Path=Path.Combine(root,"auth.json")});
        var report=new{completedAt=DateTime.UtcNow,sample,downloaded=done,cached=skipped,errors=errors.ToArray()};
        await Atomic(Path.Combine(output,sample?"muestra-informe.json":"extraccion-informe.json"),JsonSerializer.Serialize(report,Json));
        Console.WriteLine($"DETAILS_FINISHED downloaded={done} cached={skipped} errors={errors.Count}");
        if(!errors.IsEmpty)throw new InvalidOperationException("Some products require retry; see report");
    }
    static async Task<JsonObject> Download(IAPIRequestContext api,string url,string original,string output,string cache) {
        var key=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var cp=Path.Combine(cache,key+".json");
        if(File.Exists(cp)) {
            var cached=JsonNode.Parse(await File.ReadAllTextAsync(cp))!.AsObject();
            if(File.Exists(Path.Combine(output,cached["file"]!.ToString())))return cached;
        }
        var res=await Get(api,url);byte[] bytes;string mime;
        try {
            mime=res.Headers.TryGetValue("content-type",out var contentType)?contentType.Split(';')[0].Trim():"";
            if(res.Headers.TryGetValue("content-length",out var len)&&long.TryParse(len,out var length)&&length>5*1024*1024)throw new InvalidOperationException("Original image exceeds 5MB");
            bytes=await res.BodyAsync();
        }finally{await res.DisposeAsync();}
        if(bytes.Length==0)throw new InvalidOperationException("Empty source image");
        if(bytes.Length>5*1024*1024)throw new InvalidOperationException("Invalid image size");
        string ext;
        if(bytes.Length>3&&bytes[0]==0xff&&bytes[1]==0xd8&&bytes[2]==0xff){ext="jpg";mime="image/jpeg";}
        else if(bytes.Length>8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})){ext="png";mime="image/png";}
        else if(bytes.Length>12&&System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP"){ext="webp";mime="image/webp";}
        else throw new InvalidOperationException("Invalid image signature");
        var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var relative="imagenes/"+hash+"."+ext;var target=Path.Combine(output,relative);
        if(!File.Exists(target)){var temp=target+"."+Guid.NewGuid()+".tmp";await File.WriteAllBytesAsync(temp,bytes);File.Move(temp,target,true);}
        var obj=new JsonObject { ["sourceUrl"]=original,["downloadUrl"]=url,["file"]=relative,["sha256"]=hash,["mimeType"]=mime,["bytes"]=bytes.Length,["fallback"]=false };
        await Atomic(cp,obj.ToJsonString(Json));return obj;
    }
    static async Task Atomic(string path,string content) {var temp=path+"."+Guid.NewGuid()+".tmp";await File.WriteAllTextAsync(temp,content);File.Move(temp,path,true);}
}
