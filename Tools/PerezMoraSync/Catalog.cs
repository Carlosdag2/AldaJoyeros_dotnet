using AngleSharp.Html.Parser;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PerezMoraSync;

public sealed record CatalogProduct(string Reference,string Description,decimal Price,int Stock,string Category,
    string Subcategory,Dictionary<string,string> Fields,string[] ImageUrls,string? Family,string Fingerprint,bool ImageFailure=false) {
    public bool RequiresReview=>Price<=0 || Description.Length==0 || Category.Length==0 || ImageUrls.Length==0 || ImageFailure;
}
public sealed record CatalogData(IReadOnlyList<CatalogProduct> Products,string EncodingName,string FileHash) {
    public object Summary=>new {products=Products.Count,duplicates=0,outOfStock=Products.Count(x=>x.Stock==0),
        needsReview=Products.Count(x=>x.RequiresReview),missingCategory=Products.Count(x=>x.Category.Length==0),
        noImages=Products.Count(x=>x.ImageUrls.Length==0),invalidPrice=Products.Count(x=>x.Price<=0),
        imageLinks=Products.Sum(x=>x.ImageUrls.Length),uniqueImageUrls=Products.SelectMany(x=>x.ImageUrls).Distinct().Count(),encoding=EncodingName,fileHash=FileHash};
    public object Review(IEnumerable<string> failedUrls) {
        var failed=failedUrls.ToHashSet(StringComparer.Ordinal);
        return Products.Where(x=>x.RequiresReview||x.Stock==0||x.ImageUrls.Any(failed.Contains)).Select(x=>new{reference=x.Reference,stock=x.Stock,price=x.Price,category=x.Category,
            reasons=new[]{x.Stock==0?"Sin existencias":null,x.Price<=0?"Coste pendiente":null,x.Category.Length==0?"Categoría pendiente":null,
                x.Description.Length==0?"Descripción pendiente":null,x.ImageUrls.Length==0?"Sin imágenes":null,x.ImageUrls.Any(failed.Contains)?"Enlace sin imagen válida":null}.Where(r=>r!=null).ToArray(),
            failedImages=x.ImageUrls.Where(failed.Contains).ToArray()}).ToArray();
    }
}
public static class Catalog {
    public static readonly string[] Headers=["REFERENCIA","DESCRIPCION","PRECIO","STOCK","CATEGORIA","SUBCATEGORIA","METAL","COLOR ORO","TIPO","PESO G.","PIEDRA","CALIDAD PIEDRA","MEDIDAS","CIERRE","TALLA","GENERO","IMAGEN 1","IMAGEN 2","IMAGEN 3"];
    public static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string Hash(string value)=>Hash(Encoding.UTF8.GetBytes(value));
    public static bool AllowedImage(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.IsDefaultPort&&u.UserInfo.Length==0
        &&u.Host.Equals("joseperezmora.es",StringComparison.OrdinalIgnoreCase)&&u.AbsolutePath.StartsWith("/fotos/",StringComparison.Ordinal);
    public static CatalogData Parse(byte[] bytes) {
        if(bytes.Length==0 || bytes.Length>30*1024*1024)throw new InvalidDataException("El catálogo está vacío o supera 30 MB.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string html,encoding;
        try {html=new UTF8Encoding(false,true).GetString(bytes);encoding="UTF-8";}
        catch(DecoderFallbackException){html=Encoding.GetEncoding(1252).GetString(bytes);encoding="Windows-1252";}
        if(!html.TrimEnd().EndsWith("</table>",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Descarga incompleta: falta el cierre de la tabla.");
        using var doc=new HtmlParser().ParseDocument(html);
        var tables=doc.QuerySelectorAll("table");if(tables.Length!=1)throw new InvalidDataException("Se esperaba una única tabla de productos.");
        var rows=tables[0].QuerySelectorAll("tr");if(rows.Length<2)throw new InvalidDataException("El catálogo no contiene productos.");
        if(rows.Length>100001)throw new InvalidDataException("El catálogo supera 100.000 referencias. Revisar la descarga.");
        string[] Cells(AngleSharp.Dom.IElement row)=>row.Children.Where(x=>x.LocalName is "td" or "th").Select(x=>x.TextContent.Trim().Normalize(NormalizationForm.FormC)).ToArray();
        if(!Cells(rows[0]).SequenceEqual(Headers))throw new InvalidDataException("Han cambiado las columnas del proveedor. Revisar el formato antes de importar.");
        var products=new List<CatalogProduct>();var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows.Skip(1)) {
            var values=Cells(row);if(values.Length!=Headers.Length)throw new InvalidDataException("Fila incompleta en el catálogo.");
            var reference=values[0];
            if(reference.Length is 0 or >64 || reference.Any(char.IsControl)||!unique.Add(reference))throw new InvalidDataException("Referencia vacía, inválida o duplicada: "+reference);
            if(values[1].Length>1000)throw new InvalidDataException("Descripción inválida: "+reference);
            if(!decimal.TryParse(values[2],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)||price<0||price>9999999999.99m)throw new InvalidDataException("Precio inválido: "+reference);
            if(!int.TryParse(values[3],NumberStyles.None,CultureInfo.InvariantCulture,out var stock)||stock<0)throw new InvalidDataException("Stock inválido: "+reference);
            var urls=values.Skip(16).Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).ToArray();
            if(urls.Any(x=>!AllowedImage(x)))throw new InvalidDataException("Enlace de imagen fuera del proveedor: "+reference);
            var fields=Headers.Zip(values).ToDictionary(x=>x.First,x=>x.Second);
            var slash=reference.LastIndexOf('/');var family=slash>0?reference[..slash]:null;
            products.Add(new(reference,values[1],price,stock,values[4],values[5],fields,urls,family,Hash(JsonSerializer.Serialize(fields))));
        }
        // Inherit only from an explicit parent reference; never discard the size variant.
        var byRef=products.ToDictionary(x=>x.Reference,StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<products.Count;i++) {
            var p=products[i];
            if(p.Family!=null&&byRef.TryGetValue(p.Family,out var parent)) {
                p=p with {Category=p.Category.Length==0?parent.Category:p.Category,
                    Subcategory=p.Subcategory.Length==0?parent.Subcategory:p.Subcategory,
                    Description=p.Description.Length==0?parent.Description:p.Description,
                    ImageUrls=p.ImageUrls.Length==0?parent.ImageUrls:p.ImageUrls};
            }
            products[i]=p with {Fingerprint=Hash(JsonSerializer.Serialize(new{p.Fields,p.Category,p.Subcategory,p.Description,p.ImageUrls}))};
        }
        return new(products,encoding,Hash(bytes));
    }
    public static void ValidateRemovals(IReadOnlyList<CatalogProduct> products,IEnumerable<string> previous) {
        var refs=products.Select(x=>x.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var old=previous.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(products.Count==0 || old.Count(x=>!refs.Contains(x))>Math.Max(5,old.Length*.15))
            throw new InvalidDataException("Retirada superior al 15% del catálogo anterior: requiere revisión antes de aplicar cambios.");
    }
}
