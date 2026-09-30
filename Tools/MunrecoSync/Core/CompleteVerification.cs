using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;
using MongoDB.Bson;
using MongoDB.Driver;

static partial class ImportRunner {
    static async Task VerifyComplete(MySqlConnection sql,IMongoCollection<BsonDocument> images,ImportOptions o) {
        var rows=new List<(long Id,string Ref,bool Created,string State,string Manifest,int? Quantity,bool Hidden)>();
        await using(var cmd=new MySqlCommand("SELECT s.producto_id,s.referencia,s.creado_por_importacion,s.estado,s.imagenes_json,s.cantidad_proveedor,p.eliminado FROM producto_proveedor s JOIN producto p ON p.id=s.producto_id WHERE s.proveedor='Munreco'",sql)) {
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add((r.GetInt64(0),r.GetString(1),r.GetBoolean(2),r.GetString(3),r.GetString(4),r.IsDBNull(5)?null:r.GetInt32(5),r.GetBoolean(6)));
        }
        var errors=new List<string>();long imageTotal=0,verifiedImages=0;int checkedRows=0;
        foreach(var row in rows) {
            var docs=await images.Find(Builders<BsonDocument>.Filter.Eq("producto_id",row.Id)).ToListAsync();
            var expected=JsonNode.Parse(row.Manifest)!.AsArray();
            imageTotal+=docs.Count;
            if(row.State!="completo")errors.Add(row.Ref+":pending");
            if(docs.Count<expected.Count || (docs.Count==0 && !(row.Hidden && row.Quantity==0 && expected.Count==0)) || (row.Created && docs.Count!=expected.Count))errors.Add(row.Ref+":image-count");
            if(docs.Select(x=>x["orden"].ToInt32()).Distinct().Count()!=docs.Count)errors.Add(row.Ref+":duplicate-order");
            if(row.Created && docs.Count>0 && docs.Count(x=>x["es_principal"].ToBoolean())!=1)errors.Add(row.Ref+":principal");
            if(row.Created && (!row.Quantity.HasValue || row.Quantity<=0) && !row.Hidden)errors.Add(row.Ref+":unavailable-visible");
            foreach(var im in expected) {
                var doc=docs.SingleOrDefault(x=>x["_id"].ToString()==im!["mongoId"]!.ToString());
                if(doc==null){errors.Add(row.Ref+":unlinked-image");continue;}
                var bytes=doc["imagen_data"].AsBsonBinaryData.Bytes;
                if(Hash(bytes)!=im!["sha256"]!.ToString() || bytes.Length!=doc["tamano_bytes"].ToInt32())errors.Add(row.Ref+":image-content");
                else verifiedImages++;
            }
            checkedRows++;if(checkedRows%100==0)Console.WriteLine($"VERIFIED {checkedRows}/{rows.Count} images={verifiedImages}");
        }
        var backupPath=Path.Combine(o.Root,"respaldo-catalogo-antes.json");int preserved=0;
        if(File.Exists(backupPath)) {
            var backup=JsonNode.Parse(await File.ReadAllTextAsync(backupPath))!;
            foreach(var p in backup["products"]!.AsArray()) {
                await using var cmd=new MySqlCommand("SELECT nombre,descripcion,precio,categoria_id,eliminado FROM producto WHERE id=@id",sql);
                cmd.Parameters.AddWithValue("@id",p!["id"]!.GetValue<long>());
                await using var r=await cmd.ExecuteReaderAsync();
                if(!await r.ReadAsync() || r.GetString(0)!=p["reference"]!.ToString() || r.GetString(1)!=p["description"]!.ToString() || r.GetDouble(2)!=p["price"]!.GetValue<double>() || r.GetInt64(3)!=p["categoryId"]!.GetValue<long>() || r.GetBoolean(4)!=p["deleted"]!.GetValue<bool>())errors.Add("original-product-changed:"+p["id"]);
                else preserved++;
            }
        }
        long total;
        await using(var count=new MySqlCommand("SELECT COUNT(*) FROM producto",sql))total=Convert.ToInt64(await count.ExecuteScalarAsync());
        var mongoTotal=await images.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var result=new{verifiedAt=DateTime.UtcNow,supplierProducts=rows.Count,newProducts=rows.Count(x=>x.Created),existingProducts=rows.Count(x=>!x.Created),newProductsHidden=rows.Count(x=>x.Created&&x.Hidden),newProductsVisible=rows.Count(x=>x.Created&&!x.Hidden),newWithoutSupplierStock=rows.Count(x=>x.Created&&(!x.Quantity.HasValue||x.Quantity<=0)),originalProductsPreserved=preserved,totalMysqlProducts=total,totalMongoImages=mongoTotal,linkedMongoImages=imageTotal,verifiedImageContents=verifiedImages,errors};
        await File.WriteAllTextAsync(Path.Combine(o.Root,"verificacion.json"),JsonSerializer.Serialize(result,Json));
        Console.WriteLine(JsonSerializer.Serialize(result,Json));if(errors.Count>0)throw new InvalidOperationException("Verification failed");
    }
}
