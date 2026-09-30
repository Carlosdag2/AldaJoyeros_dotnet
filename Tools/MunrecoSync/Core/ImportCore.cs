using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using MySqlConnector;
using MongoDB.Bson;
using MongoDB.Driver;

record ImportOptions(string Config,string Root,bool Apply,bool Verify,int Limit,string? ImageStore=null) {
    public static ImportOptions Parse(string[] args) {
        string Value(string key,string fallback){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        return new(Value("--config",@"D:\Usuario\Campus\AldaJoyeros_dotnet\appsettings.json"),
            Path.GetFullPath(Value("--data","outputs/munreco")),args.Contains("--apply"),args.Contains("--verify"),int.Parse(Value("--limit","0")));
    }
}
record ExistingProduct(long Id,string Reference,string Description,double Price,long CategoryId,bool Deleted);
record Category(long Id,string Name);
record PlanItem(JsonObject Record,long? ExistingId,string CategoryName);

static partial class ImportRunner {
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    public static async Task Run(ImportOptions o) {
        using var settings=await RuntimeConfiguration.Read(o.Config);
        var sqlSettings=new MySqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!){ConnectionTimeout=15,DefaultCommandTimeout=60};
        await using var sql=new MySqlConnection(sqlSettings.ConnectionString);await sql.OpenAsync();
        var ms=settings.RootElement.GetProperty("MongoDbSettings");
        var mongoSettings=MongoClientSettings.FromConnectionString(ms.GetProperty("ConnectionString").GetString()!);
        mongoSettings.ServerSelectionTimeout=TimeSpan.FromSeconds(20);
        var mongo=new MongoClient(mongoSettings).GetDatabase(ms.GetProperty("DatabaseName").GetString());
        await mongo.RunCommandAsync<BsonDocument>(new BsonDocument("ping",1));
        var images=mongo.GetCollection<BsonDocument>("ProductoImagenes");
        if(o.Verify){await VerifyComplete(sql,images,o);return;}
        var existing=new Dictionary<string,ExistingProduct>(StringComparer.OrdinalIgnoreCase);
        await using(var cmd=new MySqlCommand("SELECT id,nombre,descripcion,precio,categoria_id,eliminado FROM producto",sql)) {
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())existing.Add(r.GetString(1),new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetDouble(3),r.GetInt64(4),r.GetBoolean(5)));
        }
        var categories=new List<Category>();
        await using(var cmd=new MySqlCommand("SELECT id,nombre FROM categoria",sql)) {
            await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())categories.Add(new(r.GetInt64(0),r.GetString(1)));
        }
        var files=Directory.GetFiles(Path.Combine(o.Root,"productos"),"*.json").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        if(o.Limit>0)files=files.Take(o.Limit).ToArray();
        if(files.Length==0)throw new InvalidOperationException("No downloaded products");
        var plan=new List<PlanItem>();var refs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>();
        foreach(var file in files) {
            var p=JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
            var reference=p["reference"]!.ToString();var id=p["sourceId"]!.ToString();
            if(reference.Length==0||reference.Length>100||!refs.Add(reference)||!ids.Add(id))throw new InvalidOperationException("Invalid/duplicate reference "+reference);
            if(p["currency"]?.ToString()!="EUR" || p["priceType"]?.ToString()!="PVP" || p["price"]!.GetValue<decimal>()<=0)throw new InvalidOperationException("Invalid price "+reference);
            var pics=p["images"]!.AsArray();if(pics.Count==0 && p["requiresReview"]?.GetValue<bool>()!=true)throw new InvalidOperationException("No images "+reference);
            foreach(var im in pics) {
                var path=ImagePath(o.ImageStore??o.Root,im!["file"]!.ToString());var bytes=await File.ReadAllBytesAsync(path);
                if(bytes.Length>5*1024*1024 || Hash(bytes)!=im["sha256"]!.ToString())throw new InvalidOperationException("Image validation failed "+reference);
            }
            var match=existing.GetValueOrDefault(reference);
            plan.Add(new(p,match?.Id,match==null?CategoryName(p,categories):categories.Single(x=>x.Id==match.CategoryId).Name));
        }
        var report=new {createdAt=DateTime.UtcNow,products=plan.Count,newProducts=plan.Count(x=>x.ExistingId==null),existingProducts=plan.Count(x=>x.ExistingId!=null),
            images=plan.Sum(x=>x.Record["images"]!.AsArray().Count),newCategories=plan.Select(x=>x.CategoryName).Distinct(StringComparer.OrdinalIgnoreCase).Where(x=>!categories.Any(c=>c.Name.Equals(x,StringComparison.OrdinalIgnoreCase))).ToArray(),
            preservation="Existing prices, descriptions, categories, deletion state and images are preserved",items=plan.Select(x=>new{reference=x.Record["reference"]!.ToString(),sourceId=x.Record["sourceId"]!.ToString(),existingId=x.ExistingId,category=x.CategoryName})};
        await File.WriteAllTextAsync(Path.Combine(o.Root,o.Limit>0?"plan-muestra.json":"plan-importacion.json"),JsonSerializer.Serialize(report,Json));
        Console.WriteLine($"PLAN products={plan.Count} new={report.newProducts} existing={report.existingProducts} images={report.images}");
        Console.WriteLine("NEW_CATEGORIES: "+string.Join(" | ",report.newCategories));
        if(!o.Apply)return;
        if(o.Limit==0) {
            var coverage=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(o.Root,"cobertura.json")))!;
            if(plan.Count!=coverage["products"]!.GetValue<int>())throw new InvalidOperationException("Extraction incomplete; refusing full import");
        }
        var backup=Path.Combine(o.Root,"respaldo-catalogo-antes.json");
        if(!File.Exists(backup))await File.WriteAllTextAsync(backup,JsonSerializer.Serialize(new{createdAt=DateTime.UtcNow,products=existing.Values,categories},Json));
        await Apply(sql,images,o,plan);
    }
    static string ImagePath(string root,string relative) {
        var path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Image outside data directory");
        return path;
    }
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static string CategoryName(JsonObject p,List<Category> existing) {
        var attrs=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var a in p["attributes"]!.AsArray())attrs[a!["name"]!.ToString()]=a["value"]!.ToString();
        string Attr(string name)=>attrs.GetValueOrDefault(name,"");
        var title=p["title"]!.ToString();var isWatch=p["sectionId"]!.ToString() is "11" or "13";
        if(isWatch && (title.Contains("smart",StringComparison.OrdinalIgnoreCase)||Attr("Tipo Reloj").Contains("smart",StringComparison.OrdinalIgnoreCase)))return "Smartwatch";
        var piece=isWatch?"Reloj":Attr("Tipo Pieza");
        if(piece.Length==0) {
            piece=Regex.Match(title,@"\b(pulsera|collar|pendientes|anillo|gemelos|llavero|tobillera|abalorio)\b",RegexOptions.IgnoreCase).Value;
            piece=piece.Length>0?char.ToUpperInvariant(piece[0])+piece[1..].ToLowerInvariant():"Complementos";
        }
        var material=isWatch?Attr("Material de Caja"):Attr("Material Principal");
        if(material.Length==0) {
            var found=Regex.Match(title,@"\b(?:de|en)\s+(acero|plata|piel|silicona|caucho|aluminio|ecocer\u00e1mica)\b",RegexOptions.IgnoreCase);
            if(found.Success)material=char.ToUpperInvariant(found.Groups[1].Value[0])+found.Groups[1].Value[1..].ToLowerInvariant();
        }
        if(material.StartsWith("Plata de ",StringComparison.OrdinalIgnoreCase))material="Plata";
        if(material.StartsWith("Acero ",StringComparison.OrdinalIgnoreCase))material="Acero";
        var gender=Regex.Match(title,@"\b(hombre|mujer|ni\u00f1o|ni\u00f1a|unisex)\b",RegexOptions.IgnoreCase).Value;
        if(gender.Length==0)gender=Regex.Match(p["description"]?.ToString()??"",@"\b(hombre|mujer|ni\u00f1o|ni\u00f1a|unisex)\b",RegexOptions.IgnoreCase).Value;
        if(gender.Length>0)gender=char.ToUpperInvariant(gender[0])+gender[1..].ToLowerInvariant();
        var basis=string.Join(" ",new[]{piece,material}.Where(x=>x.Length>0));
        if(isWatch && title.Contains("bolsillo",StringComparison.OrdinalIgnoreCase))basis="Reloj Bolsillo";
        var full=gender.Length>0?basis+" "+gender:basis;
        var match=existing.FirstOrDefault(x=>x.Name.Equals(full,StringComparison.OrdinalIgnoreCase))
            ?? existing.FirstOrDefault(x=>x.Name.Equals(basis,StringComparison.OrdinalIgnoreCase));
        return match?.Name ?? full;
    }
}
