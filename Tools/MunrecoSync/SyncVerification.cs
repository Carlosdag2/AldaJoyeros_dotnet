using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;
using MongoDB.Bson;
using MongoDB.Driver;

static class SyncVerification {
    public static async Task Run(string config,string output) {
        using var settings=await RuntimeConfiguration.Read(config);
        await using var sql=new MySqlConnection(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        await sql.OpenAsync();
        var before=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"respaldo-catalogo-antes.json")))!["products"]!.AsArray();
        var records=Directory.GetFiles(Path.Combine(output,"productos"),"*.json").Select(f=>JsonNode.Parse(File.ReadAllText(f))!).ToDictionary(p=>p["sourceId"]!.ToString());
        var current=new Dictionary<long,(string Reference,string Description,double Price,long Category,bool Hidden)>();
        await using(var c=new MySqlCommand("SELECT id,nombre,descripcion,precio,categoria_id,eliminado FROM producto",sql)) {
            await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())current.Add(r.GetInt64(0),(r.GetString(1),r.GetString(2),r.GetDouble(3),r.GetInt64(4),r.GetBoolean(5)));
        }
        var errors=new List<string>();var managed=new HashSet<long>();var expectedImages=new Dictionary<ObjectId,long>();int supplierRows=0;
        await using(var c=new MySqlCommand("SELECT s.referencia_externa,s.producto_id,s.creado_por_importacion,s.cantidad_proveedor,s.estado,s.imagenes_json,y.oculto,y.manual,y.motivo,p.fecha_eliminado,y.fecha_oculto,y.presente_proveedor FROM producto_proveedor s JOIN producto p ON p.id=s.producto_id LEFT JOIN producto_proveedor_sync y ON y.proveedor=s.proveedor AND y.referencia_externa=s.referencia_externa WHERE s.proveedor='Munreco'",sql)) {
            await using var r=await c.ExecuteReaderAsync();
            while(await r.ReadAsync()) {
                supplierRows++;var source=r.GetString(0);var id=r.GetInt64(1);var created=r.GetBoolean(2);if(created)managed.Add(id);
                if(r.GetString(4)!="completo")errors.Add(source+": importacion pendiente");
                var present=records.TryGetValue(source,out var record);
                if(present && (r.IsDBNull(3)||r.GetInt32(3)!=int.Parse(record!["supplierQuantity"]!.ToString())))errors.Add(source+": cantidad no actualizada");
                if(r.IsDBNull(6)){errors.Add(source+": falta control de visibilidad");continue;}
                if(r.GetBoolean(11)!=present)errors.Add(source+": presencia en catalogo incorrecta");
                if(current[id].Hidden!=r.GetBoolean(6) || (r.IsDBNull(9)?(DateTime?)null:r.GetDateTime(9))!=(r.IsDBNull(10)?(DateTime?)null:r.GetDateTime(10)))errors.Add(source+": visibilidad incoherente");
                if(created && !r.GetBoolean(7)) {
                    bool hidden=!present || record!["requiresReview"]?.GetValue<bool>()==true || int.Parse(record!["supplierQuantity"]!.ToString())<=0;
                    if(current[id].Hidden!=hidden)errors.Add(source+": disponibilidad incorrecta");
                    if(!present && r.GetString(8)!="retirado_proveedor")errors.Add(source+": retirada no registrada");
                }
                var links=JsonNode.Parse(r.GetString(5))!.AsArray();
                if(present && links.Count!=record!["images"]!.AsArray().Count)errors.Add(source+": imagenes incompletas");
                foreach(var link in links)expectedImages[ObjectId.Parse(link!["mongoId"]!.ToString())]=id;
            }
        }
        foreach(var item in before) {
            var id=item!["id"]!.GetValue<long>();
            if(!current.TryGetValue(id,out var p)){errors.Add(id+": producto previo perdido");continue;}
            if(p.Reference!=item["reference"]!.ToString() || p.Description!=item["description"]!.ToString() || p.Price!=item["price"]!.GetValue<double>() || p.Category!=item["categoryId"]!.GetValue<long>())errors.Add(id+": datos propios modificados");
            if(!managed.Contains(id) && p.Hidden!=item["deleted"]!.GetValue<bool>())errors.Add(id+": visibilidad propia modificada");
        }
        var ms=settings.RootElement.GetProperty("MongoDbSettings");
        var mongoSettings=MongoClientSettings.FromConnectionString(ms.GetProperty("ConnectionString").GetString());mongoSettings.ServerSelectionTimeout=TimeSpan.FromSeconds(20);
        var images=new MongoClient(mongoSettings).GetDatabase(ms.GetProperty("DatabaseName").GetString()).GetCollection<BsonDocument>("ProductoImagenes");
        var found=await images.Find(Builders<BsonDocument>.Filter.In("_id",expectedImages.Keys)).Project(Builders<BsonDocument>.Projection.Include("producto_id")).ToListAsync();
        if(found.Count!=expectedImages.Count)errors.Add("Faltan imagenes enlazadas en MongoDB");
        foreach(var image in found)if(image["producto_id"].ToInt64()!=expectedImages[image["_id"].AsObjectId])errors.Add("Imagen asociada a otro producto");
        var report=new{verifiedAt=DateTime.UtcNow,totalProducts=current.Count,supplierProducts=supplierRows,previousProductsPreserved=before.Count,linkedImages=found.Count,errors};
        await File.WriteAllTextAsync(Path.Combine(output,"verificacion-mensual.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"VERIFICACION productos={current.Count} referencias={supplierRows} imagenes={found.Count} errores={errors.Count}");
        if(errors.Count>0)throw new InvalidOperationException("La verificacion posterior requiere revision; consultar verificacion-mensual.json");
    }
}
