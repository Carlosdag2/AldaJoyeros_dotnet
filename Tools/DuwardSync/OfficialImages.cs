using System.Text.Json;
namespace DuwardSync;
public sealed class OfficialImages(ImageCache cache,HttpClient? transport=null) {
 readonly HttpClient http=transport??new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
 Task<Dictionary<string,string[]>>? index;
 public async Task<CatalogData> Prepare(CatalogData data) {
  var wanted=data.Products.SelectMany(p=>p.ImageUrls.Where(u=>Catalog.AllowedImage(u)&&Matches(p.Reference,u))).Distinct().ToArray();var found=await cache.Download(wanted);var products=new List<CatalogProduct>();int done=0;
  foreach(var p in data.Products) {
   var urls=p.ImageUrls.Where(u=>found.ContainsKey(u)&&Matches(p.Reference,u)).ToList();var recovered=false;
   if(urls.Count==0) {
    var lookup=await (index??=LoadIndex());
    if(lookup.TryGetValue(p.Reference,out var official)) {
     var recoveredFiles=await cache.Download(official);urls.AddRange(official.Where(recoveredFiles.ContainsKey));
    }
    // Archived references may still have their own original asset on the official CDN.
    if(urls.Count==0)foreach(var extension in new[]{"png","webp","jpg","jpeg"}) {
     var u="https://duward.com/cdn/shop/files/"+Uri.EscapeDataString(p.Reference)+"."+extension;if(p.ImageUrls.Contains(u))continue;
     if((await cache.Download([u])).ContainsKey(u)){urls.Add(u);break;}
    }
    recovered=urls.Count>0;
   }
   var valid=urls.Distinct().Take(5).ToArray();
   products.Add(p with{ImageUrls=valid,ImageFailure=valid.Length==0,Recovered=recovered,Fingerprint=Catalog.Hash(JsonSerializer.Serialize(new{fields=p.Fields.OrderBy(x=>x.Key,StringComparer.Ordinal),attributes=p.Attributes.OrderBy(x=>x.Key,StringComparer.Ordinal),images=valid}))});
   if(++done%25==0||done==data.Products.Count)Console.WriteLine($"IMAGES {done}/{data.Products.Count}");
  }
  return data with{Products=products};
 }
 public static bool Matches(string reference,string image) {
  var name=Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(new Uri(image).AbsolutePath));
  return System.Text.RegularExpressions.Regex.IsMatch(name,"(?:^|[^a-zA-Z0-9])"+System.Text.RegularExpressions.Regex.Escape(reference)+"(?:[^a-zA-Z0-9]|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 }
 async Task<Dictionary<string,string[]>> LoadIndex() {
  var result=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
  for(int page=1;page<=20;page++) {
   try {
    using var response=await http.GetAsync($"https://duward.com/products.json?limit=250&page={page}",HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
    if(response.Content.Headers.ContentLength>10*1024*1024)throw new InvalidDataException("Respuesta del catálogo demasiado grande.");
    await using var stream=await response.Content.ReadAsStreamAsync();using var memory=new MemoryStream();var b=new byte[65536];int n;
    while((n=await stream.ReadAsync(b))>0){if(memory.Length+n>10*1024*1024)throw new InvalidDataException("Respuesta demasiado grande.");await memory.WriteAsync(b.AsMemory(0,n));}
    using var json=JsonDocument.Parse(memory.ToArray());var products=json.RootElement.GetProperty("products");if(products.GetArrayLength()==0)break;
    foreach(var product in products.EnumerateArray())foreach(var variant in product.GetProperty("variants").EnumerateArray()) {
     var sku=variant.GetProperty("sku").GetString()?.Trim().ToUpperInvariant();if(string.IsNullOrEmpty(sku))continue;
     var images=product.GetProperty("images").EnumerateArray().Select(x=>x.GetProperty("src").GetString()??"").Where(Catalog.AllowedImage).Where(x=>Matches(sku,x)).ToArray();
     if(!result.TryGetValue(sku,out var list))result[sku]=list=[];list.AddRange(images);
    }
    if(products.GetArrayLength()<250)break;await Task.Delay(150);
   }catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException){Console.WriteLine("OFFICIAL Unavailable "+e.GetType().Name);break;}
  }
  return result.ToDictionary(x=>x.Key,x=>x.Value.Distinct().Take(5).ToArray(),StringComparer.OrdinalIgnoreCase);
 }
}
