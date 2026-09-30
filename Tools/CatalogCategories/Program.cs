using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Text.Json;
using System.Security.Cryptography;
using AldaJoyeros.Catalog;

if(args.Contains("--check")) {
    foreach(var pair in new[]{("Pulsera Acero Mujer","Pulseras"),("Mujer pulseras","Pulseras"),("Reloj Ecocerámica Mujer","Relojes"),("Reloj Bolsillo Hombre","Relojes de bolsillo"),("Smartwatch","Smartwatches"),("Medallas Vírgenes","Medallas"),("Mujer pendientes","Pendientes"),("9 Kilates","Oro de 9 quilates"),("Pérez Mora · Pendientes de revisar","Pérez Mora · Pendientes de revisar"),("Sin categoría","Sin categoría"),("Bebé","Bebé")})
        if(CatalogTaxonomy.Normalize(pair.Item1)!=pair.Item2 || CatalogTaxonomy.Normalize(pair.Item2)!=pair.Item2)throw new Exception("Clasificación incorrecta: "+pair.Item1);
    foreach(var pair in new[]{("Colgante Plata Mujer","Colgantes"),("Cadena Oro","Cadenas"),("Pendiente Acero","Pendientes"),("Medalla Oro","Medallas")})
        if(CatalogTaxonomy.Normalize(pair.Item1)!=pair.Item2)throw new Exception("Clasificación nueva incorrecta: "+pair.Item1);
    Console.WriteLine("15 comprobaciones de clasificación y estabilidad superadas.");return;
}
var root=Path.GetFullPath(args.FirstOrDefault()??".");
var config=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json",true).Build();
await using var sql=new MySqlConnection(config.GetConnectionString("DefaultConnection"));await sql.OpenAsync();
var pmLock="alda_perez_mora_import_"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql.Database))).ToLowerInvariant()[..16];
foreach(var key in new[]{"alda_munreco_monthly","alda_munreco_import",pmLock}) {
    await using var mutex=new MySqlCommand("SELECT GET_LOCK(@key,0)",sql);mutex.Parameters.AddWithValue("@key",key);
    if(Convert.ToInt32(await mutex.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Hay una importación en curso. Reintentar cuando termine.");
}
await using var tx=await sql.BeginTransactionAsync();
async Task<List<Dictionary<string,object?>>> Rows(string query) {
    await using var command=new MySqlCommand(query,sql,tx);await using var reader=await command.ExecuteReaderAsync();var rows=new List<Dictionary<string,object?>>();
    while(await reader.ReadAsync()){var row=new Dictionary<string,object?>();for(var i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}return rows;
}
async Task Execute(string query,params (string,object)[] parameters) {
    await using var command=new MySqlCommand(query,sql,tx);foreach(var (key,value) in parameters)command.Parameters.AddWithValue(key,value);await command.ExecuteNonQueryAsync();
}
var categories=await Rows("SELECT * FROM categoria ORDER BY id FOR UPDATE");
var products=await Rows("SELECT * FROM producto ORDER BY id FOR UPDATE");
var states=await Rows("SELECT * FROM perez_mora_product_sync ORDER BY referencia FOR UPDATE");
long Id(Dictionary<string,object?> row)=>Convert.ToInt64(row["id"]);
var changes=categories.GroupBy(c=>CatalogTaxonomy.Normalize((string)c["nombre"]!)).Select(group=>new {
    name=group.Key,group=CatalogTaxonomy.Group(group.Key),target=Id(group.FirstOrDefault(c=>(string)c["nombre"]! == group.Key)??group.First()),
    old=group.Select(c=>new{id=Id(c),name=(string)c["nombre"]!}).ToArray(),
    products=products.Count(p=>p["categoria_id"]!=null&&group.Any(c=>Id(c)==Convert.ToInt64(p["categoria_id"]))),
    visible=products.Count(p=>!Convert.ToBoolean(p["eliminado"])&&p["categoria_id"]!=null&&group.Any(c=>Id(c)==Convert.ToInt64(p["categoria_id"])))
}).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new{before=categories.Count,after=changes.Count(x=>x.products>0),groups=changes.OrderBy(c=>CatalogTaxonomy.GroupOrder(c.group)).ThenBy(c=>c.name).Select(c=>new{c.name,c.group,c.products,c.visible,merged=c.old.Length})},new JsonSerializerOptions{WriteIndented=true}));
if(!args.Contains("--apply")){await tx.RollbackAsync();return;}
var references=await Rows("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='categoria'");
if(references.Any(r=>(string)r["TABLE_NAME"]! != "producto"||(string)r["COLUMN_NAME"]! != "categoria_id"))throw new InvalidOperationException("Hay otras tablas vinculadas a categorías; revisar antes de reorganizar.");
var output=Path.Combine(root,"App_Data","CategoryOrganization",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));Directory.CreateDirectory(output);
await File.WriteAllTextAsync(Path.Combine(output,"respaldo.json"),JsonSerializer.Serialize(new{createdUtc=DateTime.UtcNow,categories,products,states,changes}));
foreach(var change in changes) {
    foreach(var old in change.old.Where(c=>c.id!=change.target)) {
        // Align the supplier baseline only when it matched the old category; preserve manual override flags.
        await Execute("UPDATE perez_mora_product_sync s JOIN producto p ON p.id=s.producto_id SET s.estado_json=JSON_SET(s.estado_json,'$.lastCategory',@new) WHERE p.categoria_id=@old AND CAST(JSON_UNQUOTE(JSON_EXTRACT(s.estado_json,'$.lastCategory')) AS UNSIGNED)=@old",("@new",change.target),("@old",old.id));
        await Execute("UPDATE producto SET categoria_id=@new WHERE categoria_id=@old",("@new",change.target),("@old",old.id));
        await Execute("DELETE FROM categoria WHERE id=@id",("@id",old.id));
    }
    await Execute("UPDATE categoria SET nombre=@name WHERE id=@id",("@name",change.name),("@id",change.target));
    if(change.products==0&&change.name!="Sin categoría")await Execute("DELETE FROM categoria WHERE id=@id",("@id",change.target));
}
var actual=await Rows("SELECT * FROM producto ORDER BY id");
string Preserved(List<Dictionary<string,object?>> rows)=>JsonSerializer.Serialize(rows.Select(r=>r.Where(x=>x.Key!="categoria_id").ToDictionary()));
if(Preserved(actual)!=Preserved(products))throw new InvalidOperationException("Cambió un dato del producto distinto de la categoría. Se cancela la operación.");
var broken=await Rows("SELECT p.id FROM producto p LEFT JOIN categoria c ON c.id=p.categoria_id WHERE p.categoria_id IS NOT NULL AND c.id IS NULL");
if(broken.Count!=0)throw new InvalidOperationException("Hay productos con una categoría inválida. Se cancela la operación.");
var finalCategories=await Rows("SELECT * FROM categoria ORDER BY nombre");
if(finalCategories.GroupBy(c=>CatalogTaxonomy.Normalize((string)c["nombre"]!)).Any(g=>g.Count()>1))throw new InvalidOperationException("Hay categorías duplicadas. Se cancela la operación.");
await tx.CommitAsync();
await File.WriteAllTextAsync(Path.Combine(output,"resultado.json"),JsonSerializer.Serialize(new{productsPreserved=actual.Count,categoriesBefore=categories.Count,categoriesAfter=finalCategories.Count,visibleCategories=changes.Count(c=>c.visible>0),categories=finalCategories}));
Console.WriteLine($"Organización aplicada: {categories.Count} → {finalCategories.Count} categorías. {actual.Count} productos conservados. Respaldo: {output}");
