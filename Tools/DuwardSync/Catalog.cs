using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
namespace DuwardSync;

public sealed record CatalogProduct(string Reference,string Description,decimal Price,int Stock,string Category,string Subcategory,
 Dictionary<string,string> Fields,string[] ImageUrls,string? Family,string Fingerprint,bool ImageFailure=false,
 Dictionary<string,string>? DisplayAttributes=null,string[]? Notices=null,bool Recovered=false) {
 public Dictionary<string,string> Attributes=>DisplayAttributes??new();
 public string[] Warnings=>Notices??[];
 public bool RequiresReview=>Price<=0||Description.Length==0||Category.Length==0||ImageUrls.Length==0||ImageFailure;
}
public sealed record CatalogData(IReadOnlyList<CatalogProduct> Products,string EncodingName,string FileHash) {
 public string[] Collections=>Products.Select(x=>x.Subcategory).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
 public int Recovered=>Products.Count(x=>x.Recovered);
 public object Summary=>new{products=Products.Count,duplicates=0,outOfStock=Products.Count(x=>x.Stock==0),needsReview=Products.Count(x=>x.RequiresReview),
  noImages=Products.Count(x=>x.ImageUrls.Length==0),invalidPrice=Products.Count(x=>x.Price<=0),dataWarnings=Products.Count(x=>x.Warnings.Length>0),collections=Collections,encoding=EncodingName,fileHash=FileHash};
 public object Review(IEnumerable<string> failed)=>Products.Where(x=>x.RequiresReview||x.Stock==0||x.Warnings.Length>0).Select(x=>new {
  reference=x.Reference,stock=x.Stock,price=x.Price,category=x.Category,
  reasons=x.Warnings.Concat(new[]{x.Stock==0?"Sin existencias":null,x.Price<=0?"PVP no válido":null,x.ImageUrls.Length==0||x.ImageFailure?"No se encontró una imagen válida de esta referencia":null}.OfType<string>()).ToArray()
 }).ToArray();
}
public static class Catalog {
 public static readonly string[] Headers=["CodigoArticulo","(Sin nombre de columna)","UnidadSaldo","MarcaProducto","CodigoAlternativo","CodigoFamilia","Descripcion","PrecioVentaconIVA2","PrecioVentasinIVA1","Genero","Pareja","Diametro","Alto","Largo","Grosor","PesoNetoUnitario_","sysFichero"];
 public static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 public static string Hash(string s)=>Hash(Encoding.UTF8.GetBytes(s));
 public static bool AllowedImage(string s)=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.IsDefaultPort&&u.UserInfo.Length==0&&(
  ((u.Host.Equals("duward.com",StringComparison.OrdinalIgnoreCase)||u.Host.Equals("www.duward.com",StringComparison.OrdinalIgnoreCase))&&u.AbsolutePath.StartsWith("/cdn/shop/files/",StringComparison.Ordinal))||
  (u.Host.Equals("cdn.shopify.com",StringComparison.OrdinalIgnoreCase)&&u.AbsolutePath.StartsWith("/s/files/1/0838/4531/6941/files/",StringComparison.Ordinal)));
 public static decimal Number(string s)=>decimal.TryParse(s.Trim().Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)?n:throw new InvalidDataException("Número no válido en el CSV: "+s);
 public static CatalogData Parse(byte[] bytes) {
  if(bytes.Length==0||bytes.Length>10*1024*1024)throw new InvalidDataException("CSV vacío o superior a 10 MB.");
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);string text,encoding;
  try{text=new UTF8Encoding(false,true).GetString(bytes);encoding="UTF-8";}catch(DecoderFallbackException){text=Encoding.GetEncoding(1252).GetString(bytes);encoding="Windows-1252";}
  text=text.TrimStart('\uFEFF');var first=text.Split('\n')[0];var separator=first.Contains(';')?";":first.Contains('\t')?"\t":",";
  using var parser=new TextFieldParser(new StringReader(text)){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=false};parser.SetDelimiters(separator);
  try {
   var headers=parser.ReadFields()?.Select(x=>x.Trim()).ToArray()??[];
   if(headers.Length>100||headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=headers.Length||Headers.Any(h=>!headers.Contains(h,StringComparer.OrdinalIgnoreCase)))throw new InvalidDataException("Las columnas no corresponden al CSV de Duward.");
   var result=new List<CatalogProduct>();var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   while(!parser.EndOfData) {
    var values=parser.ReadFields()??[];if(values.Length!=headers.Length)throw new InvalidDataException("Fila incompleta en el CSV.");
    var f=headers.Zip(values.Select(x=>x.Trim().Normalize())).ToDictionary(x=>x.First,x=>x.Second,StringComparer.OrdinalIgnoreCase);
    if(f.Values.Any(x=>x.Length>4000))throw new InvalidDataException("Campo demasiado largo en el CSV.");
    var reference=f["CodigoArticulo"].ToUpperInvariant();
    if(reference.Length>64||!Regex.IsMatch(reference,@"^D[A-Z0-9]+(?:[._-][A-Z0-9]+)*$")||!unique.Add(reference))throw new InvalidDataException("Referencia no válida o duplicada: "+reference);
    if(!f["MarcaProducto"].Equals("DUWARD",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("El archivo contiene productos de otra marca.");
    var collection=f["Descripcion"].ToUpperInvariant() switch{"SPORT"=>"Sport","AQUASTAR"=>"Aquastar","LADY"=>"Lady",_=>throw new InvalidDataException("Colección no reconocida: "+reference)};
    var price=Number(f["PrecioVentaconIVA2"]);var quantity=Number(f["UnidadSaldo"]);
    if(price<0||price>9999999999.99m||decimal.Round(price,2)!=price||quantity<0||quantity>int.MaxValue||quantity!=decimal.Truncate(quantity))throw new InvalidDataException("PVP o existencias no válidos: "+reference);
    var description=f["(Sin nombre de columna)"];if(description.Length>1000)throw new InvalidDataException("Descripción demasiado larga: "+reference);
    var notes=new List<string>();var attributes=new Dictionary<string,string>{{"Marca","DUWARD"},{"Colección",collection}};
    var key=description.ToLowerInvariant();var gender=key.Contains("unisex")?"Unisex":key.Contains("hombre")?"Hombre":key.Contains("señora")||key.Contains("mujer")?"Mujer":f["Genero"] switch{"2"=>"Hombre","3"=>"Mujer",_=>""};if(gender.Length>0)attributes["Género"]=gender;
    var ean=f["CodigoAlternativo"];if(Regex.IsMatch(ean,@"^\d{8,14}$"))attributes["Código alternativo"]=ean;else if(ean.Length>0)notes.Add("El código alternativo está abreviado o no es un código completo; no se muestra como EAN.");
    void Measure(string field,string label,decimal min,decimal max,string unit) {
     if(f[field].Length==0)return;decimal n;try{n=Number(f[field]);}catch(InvalidDataException){notes.Add(label+": valor no interpretable; se conserva en los datos originales.");return;}
     if(n==0)return;if(n<min||n>max){notes.Add(label+": valor "+f[field]+" fuera de rango; no se muestra ni se corrige por suposición.");return;}
     attributes[label]=n.ToString("0.##",CultureInfo.GetCultureInfo("es-ES"))+" "+unit;
    }
    Measure("Alto","Alto de caja",10,90,"mm");Measure("Largo","Largo de caja",10,90,"mm");Measure("Diametro","Diámetro de caja",10,70,"mm");Measure("Grosor","Grosor de caja",1,35,"mm");Measure("PesoNetoUnitario_","Peso",1,1000,"g");
    if(Regex.IsMatch(f["Pareja"],@"^D[A-Z0-9]+(?:[._-][A-Z0-9]+)*$"))attributes["Referencia de pareja"]=f["Pareja"];
    var url=f["sysFichero"];if(url.Length>0&&!AllowedImage(url))notes.Add("El enlace del CSV no es una imagen del dominio oficial; se buscará por referencia.");
    result.Add(new(reference,description,price,(int)quantity,"Relojes · Duward · "+collection,collection,f,url.Length>0?[url]:[],null,Hash(JsonSerializer.Serialize(f)),DisplayAttributes:attributes,Notices:notes.ToArray()));
    if(result.Count>100000)throw new InvalidDataException("El archivo supera 100.000 referencias.");
   }
   if(result.Count==0)throw new InvalidDataException("El CSV no contiene productos.");
   return new(result,encoding,Hash(bytes));
  }catch(MalformedLineException){throw new InvalidDataException("El CSV está incompleto o contiene comillas mal cerradas.");}
 }
 public static CatalogData Merge(IEnumerable<CatalogData> files) {
  var catalogs=files.ToArray();var products=catalogs.SelectMany(x=>x.Products).ToArray();
  var duplicates=products.GroupBy(x=>x.Reference,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1).Select(x=>x.Key).ToArray();
  if(duplicates.Length>0)throw new InvalidDataException("Hay referencias repetidas entre los archivos: "+string.Join(", ",duplicates.Take(10)));
  if(products.Length>100000)throw new InvalidDataException("El lote supera 100.000 referencias.");
  if(products.Length==0)throw new InvalidDataException("Selecciona al menos un CSV.");
  return new(products,string.Join(", ",catalogs.Select(x=>x.EncodingName).Distinct()),Hash(string.Join("|",catalogs.Select(x=>x.FileHash).Order())));
 }
 public static void ValidateRemovals(IReadOnlyList<CatalogProduct> products,IEnumerable<string> previous) {
  var refs=products.Select(x=>x.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);var old=previous.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(products.Count==0||old.Count(x=>!refs.Contains(x))>Math.Max(5,old.Length*.15))throw new InvalidDataException("Retirada superior al 15% de las colecciones incluidas: revisa que los archivos estén completos antes de aplicar.");
 }
}
