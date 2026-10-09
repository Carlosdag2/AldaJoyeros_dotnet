using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;
using MySqlConnector;
using MongoDB.Bson;
using MongoDB.Driver;
using PerezMoraSync;
using AldaJoyeros.Services.PerezMora;

var project=Path.GetFullPath(args.FirstOrDefault()??".");
var config=new ConfigurationBuilder().SetBasePath(project).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json",true).Build();
if(args.Contains("--categories-web")){await CategoryWebChecks.Run(project,config);return;}
if(args.Contains("--admin-ui-web")){await AdminUIWebChecks.Run(project,config);return;}
if(args.Contains("--featured")){await FeaturedChecks.Run(project,config);return;}
if(args.Contains("--catalog-search")){await CatalogSearchChecks.Run(project);return;}
if(args.Contains("--store-mode")){await StoreModeChecks.Run(project,config);return;}
var output=Path.Combine(project,"App_Data","PerezMora","checks");Directory.CreateDirectory(output);
if(args.Contains("--verify-real")) {
    var latest=(await new PerezMoraStore(config).Jobs()).First(x=>x.Trigger=="manual");
    if(latest.State!="completed")throw new InvalidOperationException("La importación aún no terminó: "+latest.State);
    var run=Path.Combine(project,"App_Data","PerezMora","runs",latest.Id);
    using var backup=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(run,"respaldo-catalogo.json")));
    var original=backup.RootElement.GetProperty("products").EnumerateArray().Select(x=>JsonSerializer.Deserialize<CurrentProduct>(x.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToArray();
    await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();
    var actual=new Dictionary<long,CurrentProduct>();
    await using(var cmd=new MySqlCommand("SELECT id,nombre,descripcion,precio,categoria_id,eliminado,fecha_eliminado FROM producto",sql)) {
        await using var reader=await cmd.ExecuteReaderAsync();
        while(await reader.ReadAsync())actual.Add(reader.GetInt64(0),new(reader.GetInt64(0),reader.GetString(1),reader.IsDBNull(2)?"":reader.GetString(2),Convert.ToDecimal(reader.GetValue(3)),reader.IsDBNull(4)?null:reader.GetInt64(4),reader.GetBoolean(5),reader.IsDBNull(6)?null:reader.GetDateTime(6)));
    }
    if(original.Any(x=>!actual.TryGetValue(x.Id,out var current)||current!=x))throw new InvalidDataException("Se detectó un cambio en el catálogo previo.");
    var states=new List<ManagedProduct>();
    await using(var cmd=new MySqlCommand("SELECT estado_json FROM perez_mora_product_sync",sql)){await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())states.Add(JsonSerializer.Deserialize<ManagedProduct>(reader.GetString(0),new JsonSerializerOptions(JsonSerializerDefaults.Web))!);}
    if(states.Count!=25098||states.Select(x=>x.ProductId).Distinct().Count()!=states.Count||states.Select(x=>x.Reference).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=states.Count)
        throw new InvalidDataException("No coinciden los identificadores únicos del proveedor.");
    var verifiedImagesCollection=new MongoClient(config["MongoDbSettings:ConnectionString"]!).GetDatabase(config["MongoDbSettings:DatabaseName"]).GetCollection<BsonDocument>("ProductoImagenes");
    var filter=Builders<BsonDocument>.Filter.In("producto_id",states.Select(x=>x.ProductId));
    var images=await verifiedImagesCollection.Find(filter).Project(Builders<BsonDocument>.Projection.Exclude("imagen_data")).ToListAsync();
    var ids=images.Select(x=>x["_id"].ToString()).ToHashSet();
    if(states.Any(x=>x.Images.Any(id=>!ids.Contains(id))))throw new InvalidDataException("Faltan vínculos de imágenes.");
    var duplicateImages=images.GroupBy(x=>x["producto_id"].ToInt64()).Count(g=>g.Select(x=>x["orden"].ToInt32()).Distinct().Count()!=g.Count());
    var multiplePrincipal=images.GroupBy(x=>x["producto_id"].ToInt64()).Count(g=>g.Count(x=>x["es_principal"].ToBoolean())!=1);
    if(duplicateImages>0||multiplePrincipal>0)throw new InvalidDataException("Orden o imagen principal incoherente.");
    var report=new{verifiedAt=DateTime.UtcNow,originalProductsPreserved=original.Length,imported=states.Count,totalProducts=actual.Count,hidden=states.Count(x=>x.Hidden),linkedImages=images.Count,duplicateProducts=0,duplicateImageOrders=duplicateImages,errors=Array.Empty<string>()};
    var verifiedCatalog=Catalog.Parse(await File.ReadAllBytesAsync(Path.Combine(run,"catalogo.xls")));
    var failures=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(run,"imagenes-incidencias.json")))!;
    await File.WriteAllTextAsync(Path.Combine(run,"revision.json"),JsonSerializer.Serialize(verifiedCatalog.Review(failures),new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
    await File.WriteAllTextAsync(Path.Combine(output,"real-verification.json"),JsonSerializer.Serialize(report));Console.WriteLine(JsonSerializer.Serialize(report));return;
}
if(args.Contains("--status")) {
    var jobs=await new PerezMoraStore(config).Jobs();
    Console.WriteLine(JsonSerializer.Serialize(jobs.Select(x=>new{x.Id,x.Trigger,x.State,x.Progress,x.CreatedUtc,x.EndedUtc,result=x.ResultJson==null?null:
        JsonDocument.Parse(x.ResultJson).RootElement.EnumerateObject().Where(p=>p.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)).ToDictionary(p=>p.Name,p=>p.Value.Clone())})));
    var schedule=await new PerezMoraStore(config).Schedule();Console.WriteLine(JsonSerializer.Serialize(new{weeklyEnabled=schedule.Enabled,day=schedule.Day,hour=schedule.Hour,nextUtc=schedule.NextUtc}));
    await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();
    await using var cmd=new MySqlCommand("SELECT COUNT(*),SUM(p.eliminado),SUM(pp.cantidad_proveedor=0),SUM(pp.estado='revision') FROM producto_proveedor pp JOIN producto p ON p.id=pp.producto_id WHERE pp.proveedor='PerezMora'",sql);
    await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();
    Console.WriteLine(JsonSerializer.Serialize(new{imported=reader.GetInt64(0),hidden=reader.IsDBNull(1)?0:Convert.ToInt64(reader.GetValue(1)),outOfStock=reader.IsDBNull(2)?0:Convert.ToInt64(reader.GetValue(2)),review=reader.IsDBNull(3)?0:Convert.ToInt64(reader.GetValue(3))}));return;
}
int passed=0;
void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);passed++;Console.WriteLine("PASS: "+label);}
void Reject(Action action,string label){try{action();}catch(InvalidDataException){Check(true,label);return;}throw new Exception("FAIL: "+label);}
string Html(IEnumerable<string[]> rows)=>"<table><tr>"+string.Join("",Catalog.Headers.Select(x=>"<td>"+x+"</td>"))+"</tr>"+string.Join("",rows.Select(r=>"<tr>"+string.Join("",r.Select(x=>"<td>"+System.Net.WebUtility.HtmlEncode(x)+"</td>"))+"</tr>"))+"</table>";
string[] Row(string reference,string description="Descripción Ñ",string stock="5",string category="Anillos",string price="50.25",string image="https://joseperezmora.es/fotos/test.png") {
    string[] row=Enumerable.Repeat("",19).ToArray();row[0]=reference;row[1]=description;row[2]=price;row[3]=stock;row[4]=category;row[14]=reference.Contains('/')?reference.Split('/').Last():"";row[16]=image;return row;
}
CatalogData Data(params string[][] rows)=>Catalog.Parse(Encoding.UTF8.GetBytes(Html(rows)));
var fixture=Data(Row("0024"),Row("0024/14",category:""));
Check(fixture.Products[0].Reference=="0024","Leading zeros preserved");
Check(fixture.Products[1].Reference=="0024/14"&&fixture.Products[1].Category=="Anillos","Size variants stay separate and inherit explicit parent category");
Check(Data(Row("N"),Row("Ñ")).Products.Count==2,"N and Ñ remain different identities");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Check(Catalog.Parse(Encoding.GetEncoding(1252).GetBytes(Html(new[]{Row("Ñ")}))).Products[0].Description=="Descripción Ñ","Windows-1252 accents decoded correctly");
Check(Data(Row("A",stock:"0")).Products[0].Stock==0,"Zero stock accepted");
Check(Data(Row("A",price:"0",image:"")).Products[0].RequiresReview,"Zero price or absent images require review");
Reject(()=>Data(Row("A"),Row("a")),"Duplicate references rejected");
Reject(()=>Data(Row("A",stock:"unknown")),"Invalid stock rejected");
Reject(()=>Catalog.Parse(Encoding.UTF8.GetBytes(Html(new[]{Row("A")})[..^8])),"Truncated export rejected");
Reject(()=>Data(Row("A",image:"http://127.0.0.1/private")),"Images outside provider rejected");
Reject(()=>Catalog.ValidateRemovals(fixture.Products,Enumerable.Range(1,100).Select(x=>x.ToString())),"Mass retirement blocked");
Check(PerezMoraSchedule.Next(new DateTime(2026,9,30,12,0,0),1,10)==new DateTime(2026,10,5,8,0,0),"Weekly schedule in Madrid summer time");
Check(PerezMoraSchedule.Next(new DateTime(2026,12,31,12,0,0),1,10)==new DateTime(2027,1,4,9,0,0),"Weekly schedule across year boundary");
Check(PerezMoraSchedule.Next(new DateTime(2027,3,22,0,0,0),7,2)==new DateTime(2027,3,28,1,0,0),"Weekly schedule handles nonexistent DST hour");
Check(!PerezMoraSource.ValidUrl("https://127.0.0.1/exporta_excel/1/ES/0")&&!PerezMoraSource.ValidUrl("https://perezmorajewelry.com.evil.invalid/exporta_excel/1/ES/0"),"Private addresses and lookalike domains rejected");
if(args.Contains("--parser-only")){Console.WriteLine("ALL "+passed+" CHECKS PASSED");return;}
if(args.Contains("--web")||args.Contains("--pricing-web")||args.Contains("--pagination-web")) {
    await Web();Console.WriteLine("ALL "+passed+" CHECKS PASSED");
    await File.WriteAllTextAsync(Path.Combine(output,"panel-checks.json"),JsonSerializer.Serialize(new{passed,at=DateTime.UtcNow}));return;
}

var sqlBuilder=new MySqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")!);
var testName="alda_pm_test_"+Guid.NewGuid().ToString("N");
var mongo=new MongoClient(config["MongoDbSettings:ConnectionString"]!);var mongoDb=mongo.GetDatabase(testName);
await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)){await sql.OpenAsync();await using var cmd=new MySqlCommand("CREATE DATABASE `"+testName+"`",sql);await cmd.ExecuteNonQueryAsync();}
try {
    sqlBuilder.Database=testName;
    var testConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",sqlBuilder.ConnectionString}}).Build();
    var store=new PerezMoraStore(testConfig);await store.Ensure();
    var queued=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.Enqueue("preview","test")));
    Check(queued.Count(x=>x!=null)==1,"Concurrent clicks create a single active job");
    await store.Update(queued.Single(x=>x!=null)!,"running","test");await store.RecoverInterrupted(default);
    Check((await store.Jobs()).Single().State=="interrupted","Interrupted work recovered after restart");
    await store.SaveSchedule(new(){Enabled=true,Day=1,Hour=10});
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)){await sql.OpenAsync();await using var cmd=new MySqlCommand("UPDATE perez_mora_schedule SET siguiente_utc=UTC_TIMESTAMP()-INTERVAL 8 DAY",sql);await cmd.ExecuteNonQueryAsync();}
    await store.ScheduleDue(default);await store.ScheduleDue(default);
    Check((await store.Jobs()).Count(x=>x.Active)==1,"Only one missed weekly update is queued");
    await store.Update((await store.Jobs()).Single(x=>x.Active).Id,"completed","test");
    Check((await new PerezMoraStore(testConfig).Schedule()).NextUtc>DateTime.UtcNow,"Schedule persists with next future date");
    var env=new TestEnvironment{ContentRootPath=Path.Combine(output,testName)};Directory.CreateDirectory(env.ContentRootPath);
    var protection=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(env.ContentRootPath,"keys")));
    var source=new PerezMoraSource(protection,Options.Create(new PerezMoraOptions()),env);
    var upload=Path.Combine(env.ContentRootPath,"upload.xls");await File.WriteAllTextAsync(upload,Html(new[]{Row("0024")}));
    var uploadId=await store.EnqueueUpload("preview_upload","test",upload,source,default);
    Check(uploadId!=null&&File.Exists(Path.Combine(source.Run(uploadId!),"catalogo.xls"))&&!File.Exists(upload),"Upload input ready before queue commit");
    await store.Update(uploadId!,"completed","test");
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();const string ddl="""
        CREATE TABLE categoria(id bigint AUTO_INCREMENT PRIMARY KEY,nombre varchar(100) UNIQUE NOT NULL);
        CREATE TABLE producto(id bigint AUTO_INCREMENT PRIMARY KEY,nombre varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci UNIQUE NOT NULL,descripcion varchar(1000),precio double NOT NULL,categoria_id bigint,eliminado tinyint NOT NULL,fecha_eliminado datetime NULL);
        INSERT INTO categoria(nombre) VALUES('Original');
        INSERT INTO producto(nombre,descripcion,precio,categoria_id,eliminado) VALUES('COLLISION','Producto original',123,1,0);
        """;
        await using var cmd=new MySqlCommand(ddl,sql);await cmd.ExecuteNonQueryAsync();
    }
    Environment.SetEnvironmentVariable("ALDA_PEREZMORA_CONNECTIONS",JsonSerializer.Serialize(new{ConnectionStrings=new{DefaultConnection=sqlBuilder.ConnectionString},MongoDbSettings=new{ConnectionString=config["MongoDbSettings:ConnectionString"],DatabaseName=testName}}));
    var cache=Path.Combine(output,testName,"images");Directory.CreateDirectory(cache);
    byte[] png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");
    await File.WriteAllBytesAsync(Path.Combine(cache,Catalog.Hash("https://joseperezmora.es/fotos/test.png")+".image"),png);
    int run=0;
    async Task<string> Import(CatalogData data,bool apply=true){var path=Path.Combine(output,testName,(++run).ToString());Directory.CreateDirectory(path);await Engine.Run(data,path,cache,apply,offlineImages:true);return path;}
    async Task<long> Count(string table){await using var sql=new MySqlConnection(sqlBuilder.ConnectionString);await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT COUNT(*) FROM "+table,sql);return Convert.ToInt64(await cmd.ExecuteScalarAsync());}
    var first=Data(Row("0024"),Row("0024/14",category:""),Row("SOLDOUT",stock:"0"),Row("COLLISION"));
    await Import(first,false);Check(await Count("producto")==1,"Preview does not change products");
    await Import(first);Check(await Count("producto")==4,"Initial import keeps collision unmerged");
    var imageCollection=mongoDb.GetCollection<BsonDocument>("ProductoImagenes");var pictureCount=await imageCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
    await Import(first);Check(await Count("producto")==4&&await imageCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)==pictureCount,"Repeat import creates neither product nor image duplicates");
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("UPDATE producto SET precio=99,eliminado=1,fecha_eliminado=UTC_TIMESTAMP() WHERE nombre='0024'",sql);await cmd.ExecuteNonQueryAsync();
    }
    await Import(Data(Row("0024",price:"60"),Row("0024/14",category:""),Row("SOLDOUT",stock:"5",price:"60"),Row("COLLISION")));
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT precio,eliminado FROM producto WHERE nombre='0024'",sql);await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();Check(reader.GetDouble(0)==99&&reader.GetBoolean(1),"Manual price and visibility preserved");
    }
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT eliminado FROM producto WHERE nombre='SOLDOUT'",sql);Check(!Convert.ToBoolean(await cmd.ExecuteScalarAsync()),"Restock reactivates automatically hidden product");
    }
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT precio FROM producto WHERE nombre='SOLDOUT'",sql);Check(Convert.ToDecimal(await cmd.ExecuteScalarAsync())==60m,"Provider PVP updates when price was not manually changed");
    }
    await Import(Data(Row("0024",price:"60"),Row("SOLDOUT",stock:"5"),Row("COLLISION")));
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT eliminado FROM producto WHERE nombre='0024/14'",sql);Check(Convert.ToBoolean(await cmd.ExecuteScalarAsync()),"Withdrawn managed variant hidden");
    }
    var reappeared=await Import(first);
    using(var report=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reappeared,"resultado.json"))))Check(report.RootElement.GetProperty("restocked").GetInt32()>=1,"Reappeared reference reported as restock");
    Check(await Count("producto")==4,"Reappearance uses same product ID");
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)) {
        await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT COUNT(*) FROM producto WHERE nombre='COLLISION' AND precio=123 AND eliminado=0",sql);Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==1,"Original product preserved on reference collision");
    }
    long sampleId;
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)){await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT id FROM producto WHERE nombre='0024'",sql);sampleId=Convert.ToInt64(await cmd.ExecuteScalarAsync());}
    var oldImage=(await imageCollection.Find(Builders<BsonDocument>.Filter.Eq("producto_id",sampleId)).FirstAsync())["_id"].AsObjectId;
    var manualId=ObjectId.GenerateNewId();var manualDoc=new BsonDocument{{"_id",manualId},{"producto_id",new BsonInt64(sampleId)},
        {"imagen_data",new BsonBinaryData(png.Concat(new byte[]{1}).ToArray())},{"orden",9},{"es_principal",true}};
    await imageCollection.InsertOneAsync(manualDoc);
    var newImage="https://joseperezmora.es/fotos/test2.png";
    await File.WriteAllBytesAsync(Path.Combine(cache,Catalog.Hash(newImage)+".image"),png.Concat(new byte[]{2}).ToArray());
    await Import(Data(Row("0024",image:newImage),Row("0024/14",category:""),Row("SOLDOUT",stock:"5"),Row("COLLISION")));
    Check(await imageCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id",oldImage))==0,"Changed supplier image replaces previous managed image");
    Check(await imageCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id",manualId))==1,"Manually added image remains intact");
    Check(await imageCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("producto_id",sampleId)&Builders<BsonDocument>.Filter.Eq("es_principal",true))==1,"Manual principal image retained without duplicate principals");
    Check(AldaJoyeros.Catalog.ProviderPricing.TryCoefficient("2,5",out var parsedFactor)&&parsedFactor==2.5m,"Spanish decimal coefficient accepted");
    Check(!AldaJoyeros.Catalog.ProviderPricing.TryCoefficient("0",out _)&&!AldaJoyeros.Catalog.ProviderPricing.TryCoefficient("-2",out _)&&!AldaJoyeros.Catalog.ProviderPricing.TryCoefficient("101",out _),"Invalid coefficients rejected");
    Check(AldaJoyeros.Catalog.ProviderPricing.Pvp(1.01m,2.5m)==2.53m,"PVP rounds midpoint away from zero");
    var bands=new AldaJoyeros.Catalog.ProviderPriceBands(2m,3m,4m);
    Check(bands.Factor(0)==2&&bands.Factor(100)==2&&bands.Factor(100.01m)==3&&bands.Factor(500)==3&&bands.Factor(500.01m)==4,"Cost bands cover exact boundaries and cents without gaps");
    async Task<decimal> Price(string reference){await using var sql=new MySqlConnection(sqlBuilder.ConnectionString);await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT precio FROM producto WHERE nombre=@ref",sql);cmd.Parameters.AddWithValue("@ref",reference);return Convert.ToDecimal(await cmd.ExecuteScalarAsync());}
    var pricingBefore=await Price("0024/14");
    var pricePreview=await store.Prices(2.5m,false,env.ContentRootPath);
    Check(pricePreview.Updated==2&&pricePreview.Protected==1&&await Price("0024/14")==pricingBefore&&await store.Coefficient()==1m,"Price preview changes neither PVP nor stored coefficient");
    await store.Prices(2.5m,true,env.ContentRootPath);
    Check(await Price("0024/14")==125.63m&&await Price("0024")==99&&await Price("COLLISION")==123,"Coefficient applies only to owned automatic prices");
    await store.Prices(2.5m,true,env.ContentRootPath);
    Check(await Price("0024/14")==125.63m,"Repeated coefficient application never multiplies the previous PVP");
    await store.Prices(3m,true,env.ContentRootPath);
    Check(await Price("0024/14")==pricingBefore*3m,"Changing coefficient uses original supplier cost");
    Check(await new PerezMoraStore(testConfig).Coefficient()==3m,"Coefficient persists after restart");
    await Import(Data(Row("0024",image:newImage),Row("0024/14",category:"",price:"70"),Row("SOLDOUT",stock:"5"),Row("COLLISION"),Row("NEWPRICE",price:"1.01")));
    Check(await Price("0024/14")==210m&&await Price("NEWPRICE")==3.03m&&await Price("0024")==99m,"Future import prices existing and new products from cost using saved coefficient");
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)){await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT pvp FROM producto_proveedor WHERE referencia='0024/14'",sql);Check(Convert.ToDecimal(await cmd.ExecuteScalarAsync())==70m,"Original supplier cost remains unchanged");}
    // Simular una instalación antigua únicamente en la base de pruebas desechable.
    await using(var sql=new MySqlConnection(sqlBuilder.ConnectionString)){await sql.OpenAsync();await using var cmd=new MySqlCommand("DROP TABLE perez_mora_pricing_bands",sql);await cmd.ExecuteNonQueryAsync();}
    Check(await new PerezMoraStore(testConfig).Bands()==new AldaJoyeros.Catalog.ProviderPriceBands(3,3,3),"Legacy coefficient is preserved across all bands on schema migration");
    await Import(Data(Row("0024",image:newImage),Row("0024/14",price:"100"),Row("SOLDOUT",stock:"5",price:"100.01"),Row("COLLISION"),Row("NEWPRICE",price:"500"),Row("HIGH",price:"500.01")));
    var beforeBands=await Price("NEWPRICE");
    var bandPreview=await store.Prices(bands,false,env.ContentRootPath);
    Check(bandPreview.Bands==bands&&await Price("NEWPRICE")==beforeBands&&await store.Bands()==new AldaJoyeros.Catalog.ProviderPriceBands(3,3,3),"Band preview changes neither prices nor configuration");
    await store.Prices(bands,true,env.ContentRootPath);
    Check(await Price("0024/14")==200&&await Price("SOLDOUT")==300.03m&&await Price("NEWPRICE")==1500&&await Price("HIGH")==2000.04m,"Recosting uses the correct band at each boundary");
    Check(await Price("0024")==99&&await Price("COLLISION")==123,"Band recosting preserves manual and foreign prices");
    await store.Prices(bands,true,env.ContentRootPath);
    Check(await Price("HIGH")==2000.04m&&await new PerezMoraStore(testConfig).Bands()==bands,"Bands persist after restart and repeated application does not compound prices");
    await Import(Data(Row("0024",image:newImage),Row("0024/14",price:"100"),Row("SOLDOUT",stock:"5",price:"100.01"),Row("COLLISION"),Row("NEWPRICE",price:"501"),Row("HIGH",price:"99"),Row("NEWBAND",price:"600")));
    Check(await Price("NEWPRICE")==2004&&await Price("HIGH")==198&&await Price("NEWBAND")==2400,"Future imports price new products and move existing products between cost bands");
}finally {
    Environment.SetEnvironmentVariable("ALDA_PEREZMORA_CONNECTIONS",null);
    if(!testName.StartsWith("alda_pm_test_")||testName.Length!=45)throw new Exception("Unsafe test cleanup name");
    await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();await using var cmd=new MySqlCommand("DROP DATABASE `"+testName+"`",sql);await cmd.ExecuteNonQueryAsync();await mongo.DropDatabaseAsync(testName);
}
Console.WriteLine("ALL "+passed+" CHECKS PASSED");
await File.WriteAllTextAsync(Path.Combine(output,"engine-checks.json"),JsonSerializer.Serialize(new{passed,at=DateTime.UtcNow}));

async Task Web() {
    string B64(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    string Token(string role) {
        var head=B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var body=B64(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,object>{{"iss",config["JwtSettings:Issuer"]!},{"aud",config["JwtSettings:Audience"]!},
            {"exp",DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds()},{"nbf",DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()},
            {"nameid","1"},{"userId","1"},{"email","local-verification@example.invalid"},{"role",role},{"rol",role}}));
        var data=head+"."+body;using var hmac=new HMACSHA256(Encoding.ASCII.GetBytes(config["JwtSettings:Secret"]!));return data+"."+B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(data)));
    }
    var url=args.Contains("--pagination-web")?"http://127.0.0.1:5094":args.Contains("--pricing-web")?"http://127.0.0.1:5093":"http://127.0.0.1:5091";
    using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Channel="msedge",Headless=true});
    await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=1440,Height=1000}});
    var page=await context.NewPageAsync();var errors=new List<string>();page.PageError+=(_,e)=>errors.Add(e);
    await page.GotoAsync(url+"/AdminPerezMora");Check(page.Url.Contains("/Auth/Login"),"Anonymous user cannot open Perez Mora panel");
    await context.AddCookiesAsync([new(){Name="jwt_token",Value=Token("USER"),Url=url}]);
    Check((await context.APIRequest.GetAsync(url+"/AdminPerezMora")).Status==403,"Non-admin cannot open Perez Mora panel");
    await context.AddCookiesAsync([new(){Name="jwt_token",Value=Token("ADMIN"),Url=url}]);
    Check((await page.GotoAsync(url+"/AdminPerezMora"))?.Status==200,"Admin panel renders");
    if(args.Contains("--pagination-web")) {
        var low=AldaJoyeros.Helpers.PagedResult<int>.Create(Enumerable.Range(1,31),0,12);
        var high=AldaJoyeros.Helpers.PagedResult<int>.Create(Enumerable.Range(1,31),int.MaxValue,12);
        var empty=AldaJoyeros.Helpers.PagedResult<int>.Create(Array.Empty<int>(),20,12);
        Check(low.PageNumber==1&&high.PageNumber==3&&high.Items.Count==7&&empty.StartItem==0,"Server clamps invalid page numbers and handles empty lists");
        await page.GotoAsync(url+"/Productos?categoriaId=11");
        await page.Locator("#paginacion [data-page-rail] button").First.WaitForAsync();
        Check(await page.Locator("form[data-page-jump]").CountAsync()==0,"Separate page input removed");
        var viewport=page.Locator("#paginacion [data-page-viewport]");
        await viewport.ScrollIntoViewIfNeededAsync();
        var box=(await viewport.BoundingBoxAsync())!;
        var beforeDrag=page.Url;
        await page.Mouse.MoveAsync(box.X+box.Width-20,box.Y+15);await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X+20,box.Y+15,new(){Steps=10});await page.Mouse.UpAsync();
        Check(await viewport.EvaluateAsync<double>("el => el.scrollLeft")>0&&page.Url==beforeDrag,"Dragging the number row scrolls without changing the page");
        await page.WaitForTimeoutAsync(350);
        var total=int.Parse((await page.Locator("#paginacion").GetAttributeAsync("data-total-pages"))!);
        async Task CatalogPage(int number) {
            await page.Locator("#paginacion [data-page-viewport]").EvaluateAsync(" (el, n) => el.scrollLeft = (n-1)*48",number);
            var button=page.Locator("#paginacion [data-page-rail] button[data-page='"+number+"']");
            await button.WaitForAsync();
            await page.RunAndWaitForResponseAsync(async()=>await button.ClickAsync(),r=>r.Url.Contains("/Productos/Filtrar"));
            await page.WaitForFunctionAsync("n => document.querySelector('#paginacion')?.dataset.currentPage === String(n)",number);
        }
        await CatalogPage(Math.Min(200,total));
        Check(page.Url.Contains("categoriaId=11"),"Clicking a distant page preserves the category");
        await CatalogPage(total);
        Check(await page.Locator("#paginacion [aria-label='Página siguiente']").IsDisabledAsync(),"Last page reachable by sliding the same row");
        await CatalogPage(3);
        Check(await page.Locator("#paginacion [data-page-rail] button").CountAsync()<30,"Thousands of page numbers do not create thousands of DOM elements");
        await page.GotoAsync(url+"/AdminProductos?filtro=todos");
        await page.Locator("#paginacionProductos [data-page-rail] button").First.WaitForAsync();
        await page.Locator("#paginacionProductos [data-page-viewport]").EvaluateAsync("el => el.scrollLeft = 19*48");
        var adminButton=page.Locator("#paginacionProductos [data-page-rail] button[data-page='20']");
        await adminButton.WaitForAsync();
        await page.RunAndWaitForResponseAsync(async()=>await adminButton.ClickAsync(),r=>r.Url.Contains("/AdminProductos")&&r.Request.Method=="GET");
        await page.WaitForFunctionAsync("() => document.querySelector('#paginacionProductos')?.dataset.currentPage === '20'");
        await page.WaitForURLAsync(u=>u.Contains("page=20"));
        Check(page.Url.Contains("filtro=todos"),"Admin row preserves selected filter");
        foreach(var route in new[]{"AdminUsuarios","AdminPedidos"})Check((await page.GotoAsync(url+"/"+route))?.Status==200,"Pagination view renders: "+route);
        await page.SetViewportSizeAsync(390,844);await page.GotoAsync(url+"/Productos?categoriaId=11&page=200");
        await page.Locator("#paginacion [aria-current='page']").WaitForAsync();
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"),"Scrollable row fits mobile viewport");
        await page.Locator("#paginacion").ScreenshotAsync(new(){Path=Path.Combine(output,"paginado-deslizable.png")});
        Check(errors.Count==0,"Pagination has no JavaScript errors");return;
    }
    if(args.Contains("--pricing-web")) {
        async Task<string> PricesHash() {
            await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();
            await using var cmd=new MySqlCommand("SELECT id,precio FROM producto ORDER BY id",sql);await using var reader=await cmd.ExecuteReaderAsync();
            var rows=new List<string>();while(await reader.ReadAsync())rows.Add(reader.GetInt64(0)+":"+Convert.ToDecimal(reader.GetValue(1)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",rows))));
        }
        var priceStore=new PerezMoraStore(config);var factorBefore=await priceStore.Coefficient();var pricesBefore=await PricesHash();
        var unprotectedPrice=context.APIRequest.CreateFormData();unprotectedPrice.Set("coeficienteA","2.5");unprotectedPrice.Set("coeficienteB","2.5");unprotectedPrice.Set("coeficienteC","2.5");
        Check((await context.APIRequest.PostAsync(url+"/AdminPerezMora/Precios",new(){Form=unprotectedPrice})).Status==400,"Price changes require antiforgery token");
        await page.Locator("#pricing-coefficient-A").FillAsync("2.5");await page.Locator("#pricing-coefficient-B").FillAsync("2.5");await page.Locator("#pricing-coefficient-C").FillAsync("2.5");
        await page.GetByRole(AriaRole.Button,new(){Name="Previsualizar precios",Exact=true}).ClickAsync();
        await page.Locator("#pricing-preview").WaitForAsync();
        Check(await page.Locator("#pricing-preview").IsVisibleAsync(),"Real catalog price preview renders");
        Check(pricesBefore==await PricesHash()&&factorBefore==await priceStore.Coefficient(),"Preview preserves every real price and the stored coefficient");
        var priceToken=await page.Locator("#pricing-section input[name='__RequestVerificationToken']").InputValueAsync();
        var invalidPrice=context.APIRequest.CreateFormData();invalidPrice.Set("coeficienteA","0");invalidPrice.Set("coeficienteB","2.5");invalidPrice.Set("coeficienteC","2.5");invalidPrice.Set("aplicar","true");invalidPrice.Set("__RequestVerificationToken",priceToken);
        await context.APIRequest.PostAsync(url+"/AdminPerezMora/Precios",new(){Form=invalidPrice});
        Check(pricesBefore==await PricesHash()&&factorBefore==await priceStore.Coefficient(),"Invalid coefficient leaves all real prices unchanged");
        await page.Locator("#pricing-section").ScreenshotAsync(new(){Path=Path.Combine(output,"precios-panel.png")});
        await page.SetViewportSizeAsync(390,844);await page.GotoAsync(url+"/AdminPerezMora");
        Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"),"Price panel fits mobile viewport");
        Check(errors.Count==0,"Price panel has no JavaScript errors");return;
    }
    foreach(var job in (await new PerezMoraStore(config).Jobs()).Where(x=>x.State is "completed" or "previewed").Take(2)) {
        var report=await context.APIRequest.GetAsync(url+"/AdminPerezMora/Informe?id="+job.Id+"&tipo="+(job.State=="completed"?"resultado":"comparacion"));
        Check(report.Status==200,"Completed protected report downloadable");
        if(job.State=="completed")Check((await context.APIRequest.GetAsync(url+"/AdminPerezMora/Informe?id="+job.Id+"&tipo=revision")).Status==200,"Hidden-product review report downloadable");
    }
    foreach(var action in new[]{"Actualizar","Comparar","Programar","Conectar","Subir"})Check((await context.APIRequest.PostAsync(url+"/AdminPerezMora/"+action)).Status==400,action+" requires antiforgery token");
    var csrf=await page.Locator("form[action='/AdminPerezMora/Programar'] input[name='__RequestVerificationToken']").InputValueAsync();
    var scheduleBefore=await new PerezMoraStore(config).Schedule();
    var invalid=context.APIRequest.CreateFormData();invalid.Set("__RequestVerificationToken",csrf);invalid.Set("Day","8");invalid.Set("Hour","25");
    await context.APIRequest.PostAsync(url+"/AdminPerezMora/Programar",new(){Form=invalid});
    var scheduleAfter=await new PerezMoraStore(config).Schedule();Check(scheduleBefore.Enabled==scheduleAfter.Enabled&&scheduleBefore.NextUtc==scheduleAfter.NextUtc,"Invalid weekly schedule leaves stored settings unchanged");
    var privateUrl=Environment.GetEnvironmentVariable("ALDA_PM_DOWNLOAD_URL");
    if(privateUrl!=null) {
        await page.ReloadAsync();var form=context.APIRequest.CreateFormData();
        form.Set("__RequestVerificationToken",await page.Locator("form[action='/AdminPerezMora/Conectar'] input[name='__RequestVerificationToken']").InputValueAsync());form.Set("DownloadUrl",privateUrl);
        var response=await context.APIRequest.PostAsync(url+"/AdminPerezMora/Conectar",new(){Form=form,Timeout=120000});Check(response.Status==200,"Official download connected through protected panel action");
        Check(File.Exists(Path.Combine(project,"App_Data","PerezMora","download.protected")),"Private download link saved encrypted");
        Check(!(await File.ReadAllTextAsync(Path.Combine(project,"App_Data","PerezMora","download.protected"))).Contains(privateUrl),"Saved connection contains no plaintext download URL");
        await page.ReloadAsync();Check(await page.GetByText("Enlace guardado",new(){Exact=true}).IsVisibleAsync(),"Connected state visible in panel");
    }
    if(args.Contains("--preview")) {
        await page.Locator("#preview-button").ClickAsync();await page.WaitForURLAsync(url+"/AdminPerezMora");
        var jobs=await new PerezMoraStore(config).Jobs();Check(jobs.Any(x=>x.Trigger=="preview"&&x.Active),"Panel queues a read-only live comparison");
        Console.WriteLine("REAL_PREVIEW_JOB "+jobs.First().Id);
    }
    await page.ReloadAsync();await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-desktop.png"),FullPage=true});
    await page.SetViewportSizeAsync(390,844);Check(!await page.EvaluateAsync<bool>("document.documentElement.scrollWidth>window.innerWidth"),"Panel fits mobile viewport");
    await page.ScreenshotAsync(new(){Path=Path.Combine(output,"panel-mobile.png"),FullPage=true});Check(errors.Count==0,"Panel has no browser script errors");
    if(args.Contains("--shop")) {
        long id;
        await using(var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"))) {
            await sql.OpenAsync();await using var cmd=new MySqlCommand("SELECT p.id FROM producto p JOIN producto_proveedor pp ON pp.producto_id=p.id WHERE pp.proveedor='PerezMora' AND p.eliminado=0 AND pp.estado='completo' ORDER BY p.id LIMIT 1",sql);
            id=Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        await page.SetViewportSizeAsync(1440,1000);Check((await page.GotoAsync(url+"/Productos/Detalle/"+id))?.Status==200,"Imported product detail renders");
        Check(await page.GetByText("Pérez Mora").CountAsync()>0,"Provider displayed on public product detail");
        Check(await page.GetByText("PESO G.",new(){Exact=true}).CountAsync()>0,"Imported characteristics displayed on product detail");
        Check(await page.Locator("img[src^='data:image/']").CountAsync()>0,"Imported MongoDB images displayed on product detail");
        await page.WaitForFunctionAsync("() => Number(getComputedStyle(document.querySelector('.animate-fade-in-up')).opacity) > 0.99");
        await page.ScreenshotAsync(new(){Path=Path.Combine(output,"producto-perez-mora.png"),FullPage=true});
        Check((await page.GotoAsync(url+"/Productos"))?.Status==200,"Public product catalog renders after large import");
    }
    if(args.Contains("--apply")) {
        var jobs=await new PerezMoraStore(config).Jobs();if(jobs.Any(x=>x.Active))throw new InvalidOperationException("Finish preview before requesting import.");
        var form=context.APIRequest.CreateFormData();form.Set("__RequestVerificationToken",await page.Locator("form[action='/AdminPerezMora/Actualizar'] input[name='__RequestVerificationToken']").InputValueAsync());
        await context.APIRequest.PostAsync(url+"/AdminPerezMora/Actualizar",new(){Form=form});
        jobs=await new PerezMoraStore(config).Jobs();Check(jobs.Any(x=>x.Active&&x.Trigger=="manual"),"Requested real supplier import queued in panel");Console.WriteLine("REAL_IMPORT_JOB "+jobs.First().Id);
    }
}

sealed class TestEnvironment:IWebHostEnvironment {
    public string ApplicationName { get; set; }="PerezMoraChecks";
    public string EnvironmentName { get; set; }="Development";
    public string ContentRootPath { get; set; }="";
    public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    public string WebRootPath { get; set; }=null!;
    public IFileProvider WebRootFileProvider { get; set; }=new NullFileProvider();
}
