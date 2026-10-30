using System.Net;
using System.Text;
using System.Text.Json;
using DuwardSync;
using SkiaSharp;
int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
void Reject(Action action,string name){try{action();throw new Exception("Expected rejection: "+name);}catch(InvalidDataException){Check(true,name);}}
var root=Path.GetFullPath(args.FirstOrDefault()??throw new ArgumentException("Indica una carpeta de pruebas."));Directory.CreateDirectory(root);
var sources=args.Skip(1).Where(x=>x.EndsWith(".csv",StringComparison.OrdinalIgnoreCase)).ToArray();
var files=sources.Select(x=>Catalog.Parse(File.ReadAllBytes(x))).ToArray();var all=Catalog.Merge(files);
Check(all.Products.Count==333&&all.Products.Select(x=>x.Reference).Distinct().Count()==333,"333 referencias únicas de los tres CSV originales");
Check(all.Collections.SequenceEqual(new[]{"Aquastar","Lady","Sport"}),"Colecciones reconocidas sin depender del nombre del archivo");
Check(all.Products.Single(x=>x.Reference=="D14023.10").Price==49.9m,"PVP con IVA y coma decimal; no se usa el precio sin IVA");
Check(all.Products.Single(x=>x.Reference=="D85516.05").Stock==12,"Existencias originales conservadas");
Check(all.Products.Single(x=>x.Reference=="D13073.12").Warnings.Any(x=>x.StartsWith("Diámetro"))&&!all.Products.Single(x=>x.Reference=="D13073.12").Attributes.ContainsKey("Diámetro de caja"),"No se publica ni se inventa un diámetro de 297 mm");
Check(all.Products.Any(x=>x.Warnings.Any(w=>w.Contains("abreviado"))),"Códigos en notación científica no se convierten a EAN inventados");
Reject(()=>Catalog.Merge(new[]{files[0],files[0]}),"Rechazo de duplicados entre archivos");
var fields=new Dictionary<string,string>(all.Products[0].Fields,StringComparer.OrdinalIgnoreCase);fields["CodigoArticulo"]="D99999.01";fields["Descripcion"]="Sport";fields["(Sin nombre de columna)"]="Reloj \"especial\";\nSeñora";fields["PrecioVentaconIVA2"]="49,90";fields["UnidadSaldo"]="2";fields["sysFichero"]="https://duward.com/cdn/shop/files/D99999.01.jpg";
string Escape(string x)=>"\""+x.Replace("\"","\"\"")+"\"";
byte[] Csv(params Dictionary<string,string>[] rows)=>Encoding.UTF8.GetBytes(string.Join(';',Catalog.Headers)+"\n"+string.Join("\n",rows.Select(f=>string.Join(';',Catalog.Headers.Select(h=>Escape(f[h]))))));
var data=Catalog.Parse(Csv(fields));Check(data.Products[0].Description.Contains("\"especial\";\nSeñora"),"CSV con comillas, punto y coma y saltos de línea dentro de una celda");
Reject(()=>Catalog.Parse(Csv(fields,fields)),"Rechazo de duplicados dentro del CSV");
var invalid=new Dictionary<string,string>(fields);invalid["UnidadSaldo"]="-1";Reject(()=>Catalog.Parse(Csv(invalid)),"Stock negativo rechazado");
invalid["UnidadSaldo"]="1.5";Reject(()=>Catalog.Parse(Csv(invalid)),"Stock fraccionario rechazado");
invalid["UnidadSaldo"]="1";invalid["PrecioVentaconIVA2"]="1,001";Reject(()=>Catalog.Parse(Csv(invalid)),"PVP con más de dos decimales rechazado");invalid["PrecioVentaconIVA2"]="49,90";invalid["UnidadSaldo"]="1";invalid["Descripcion"]="Otra";Reject(()=>Catalog.Parse(Csv(invalid)),"Colección desconocida rechazada");
Reject(()=>Catalog.Parse(Encoding.UTF8.GetBytes("CodigoArticulo;otro\nD1;2")),"Cambio de formato detectado antes de guardar");
Check(!Catalog.AllowedImage("http://127.0.0.1/secrets")&&!Catalog.AllowedImage("https://duward.com.evil.test/cdn/shop/files/a.png")&&!Catalog.AllowedImage("https://cdn.shopify.com/s/files/otro/a.png"),"Solo se descargan imágenes de Duward y de su espacio oficial en Shopify");
Check(OfficialImages.Matches("D99999.01","https://duward.com/cdn/shop/files/D99999.01_1.png")&&!OfficialImages.Matches("D99999.01","https://duward.com/cdn/shop/files/D99999.010.png"),"La coincidencia de imagen exige la referencia completa");
var fresh=data.Products[0];var old=new ManagedProduct(1,fresh.Reference,"",2,true,false,null,49.9m,"Original",1,false,false,false,false,[],true,"Sport");var current=new CurrentProduct(1,fresh.Reference,"Original",49.9m,1,false,null);
Check(Policy.Decide(old,current,fresh with{Stock=0}).Hidden,"Los agotados se ocultan");
Check(Policy.Decide(old,current,fresh with{ImageUrls=[],ImageFailure=true}).Hidden,"Los productos sin foto válida se ocultan");
Check(Policy.Decide(old,current with{Price=90,Description="Manual",Category=2},fresh) is{ManualPrice:true,ManualDescription:true,ManualCategory:true},"Se protegen precio, descripción y categoría cambiados por el administrador");
Check(Policy.Decide(old,current with{Hidden=true,DeletedAt=DateTime.UtcNow},fresh).Hidden,"Se conserva una ocultación manual");
Reject(()=>Catalog.ValidateRemovals(data.Products,Enumerable.Range(0,100).Select(i=>"D"+i)),"Bloqueo de una retirada masiva por archivo incompleto");
using var bitmap=new SKBitmap(2,2);bitmap.Erase(SKColors.Green);using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);var photo=png.ToArray();
Check(ImageCache.Mime(photo)=="image/png","Se valida una imagen decodificable");Reject(()=>ImageCache.Mime(Encoding.UTF8.GetBytes("<html>No es imagen</html>")),"HTML rechazado aunque la URL termine en JPG");
var mock=new Mock(photo);var client=new HttpClient(mock);var cache=new ImageCache(Path.Combine(root,"cache"),transport:client);var recovered=await new OfficialImages(cache,client).Prepare(data);
Check(recovered.Products[0].Recovered&&recovered.Products[0].ImageUrls.Single().Contains("D99999.01.png"),"Un JPG roto recupera la imagen oficial del mismo SKU");
Check(!recovered.Products[0].RequiresReview,"La imagen recuperada evita ocultar una ficha completa");
Check(mock.Requests.All(u=>u.Host=="duward.com"||u.Host=="cdn.shopify.com"),"La recuperación no consulta dominios arbitrarios");
var wrong=data with{Products=data.Products.Select(x=>x with{ImageUrls=["https://cdn.shopify.com/s/files/1/0838/4531/6941/files/D99999.02.png"]}).ToArray()};
var corrected=await new OfficialImages(new ImageCache(Path.Combine(root,"wrong-photo"),transport:client),client).Prepare(wrong);
Check(corrected.Products[0].ImageUrls.All(x=>OfficialImages.Matches("D99999.01",x)),"Un enlace de otro color en el CSV tampoco se acepta como imagen de la referencia");
Console.WriteLine($"OK {passed} comprobaciones");
sealed class Mock(byte[] photo):HttpMessageHandler {
 public List<Uri> Requests{get;}=[];
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  var u=request.RequestUri!;Requests.Add(u);HttpResponseMessage r;
  if(u.AbsolutePath=="/products.json")r=new(HttpStatusCode.OK){Content=new StringContent("""{"products":[{"variants":[{"sku":"D99999.01"}],"images":[{"src":"https://cdn.shopify.com/s/files/1/0838/4531/6941/files/D99999.01.png"}]},{"variants":[{"sku":"D99999.02"}],"images":[{"src":"https://cdn.shopify.com/s/files/1/0838/4531/6941/files/D99999.02.png"}]}]}""")};
  else if(u.AbsolutePath.EndsWith("D99999.01.png"))r=new(HttpStatusCode.OK){Content=new ByteArrayContent(photo)};
  else r=new(HttpStatusCode.NotFound);
  return Task.FromResult(r);
 }
}
