using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using MySqlConnector;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;

static partial class ImportRunner {
    static async Task Apply(MySqlConnection sql,IMongoCollection<BsonDocument> images,ImportOptions o,List<PlanItem> plan) {
        await using(var lockCmd=new MySqlCommand("SELECT GET_LOCK('alda_munreco_import',0)",sql))
            if(Convert.ToInt32(await lockCmd.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Another import is running");
        var results=new List<object>();
        try {
            const string ddl="""
            CREATE TABLE IF NOT EXISTS producto_proveedor (
              proveedor varchar(32) NOT NULL,
              referencia_externa varchar(64) NOT NULL,
              referencia varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
              producto_id bigint NOT NULL,
              marca varchar(100) NOT NULL,
              seccion varchar(100) NOT NULL,
              coleccion varchar(200) NOT NULL,
              descripcion_completa longtext NOT NULL,
              pvp decimal(12,2) NOT NULL,
              moneda varchar(3) NOT NULL,
              disponibilidad_proveedor varchar(100) NOT NULL,
              cantidad_proveedor int NULL,
              caracteristicas_json json NOT NULL,
              datos_json json NOT NULL,
              imagenes_json json NOT NULL,
              url_origen varchar(1000) NOT NULL,
              estado varchar(30) NOT NULL,
              creado_por_importacion tinyint(1) NOT NULL,
              fecha_captura datetime(6) NOT NULL,
              fecha_importacion datetime(6) NOT NULL,
              PRIMARY KEY(proveedor,referencia_externa),
              UNIQUE KEY uq_proveedor_referencia(proveedor,referencia),
              KEY idx_producto_proveedor_producto(producto_id),
              CONSTRAINT fk_producto_proveedor_producto FOREIGN KEY(producto_id) REFERENCES producto(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """;
            await using(var cmd=new MySqlCommand(ddl,sql))await cmd.ExecuteNonQueryAsync();
            var schemaBackup=new List<object>();
            foreach(var spec in new[]{(Table:"producto",Column:"nombre"),(Table:"producto_proveedor",Column:"referencia")}) {
                string? collation;
                await using(var c=new MySqlCommand("SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table AND COLUMN_NAME=@column",sql)) {
                    c.Parameters.AddWithValue("@table",spec.Table);c.Parameters.AddWithValue("@column",spec.Column);
                    collation=Convert.ToString(await c.ExecuteScalarAsync());
                }
                schemaBackup.Add(new{table=spec.Table,column=spec.Column,previousCollation=collation});
                if(collation!="utf8mb4_0900_as_ci") {
                    await using var alter=new MySqlCommand($"ALTER TABLE `{spec.Table}` MODIFY `{spec.Column}` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL",sql);
                    await alter.ExecuteNonQueryAsync();
                }
            }
            var schemaPath=Path.Combine(o.Root,"respaldo-esquema-referencias.json");
            if(!File.Exists(schemaPath))await File.WriteAllTextAsync(schemaPath,JsonSerializer.Serialize(schemaBackup,Json));

            await using(var rule=new MySqlCommand("SELECT DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='producto_proveedor' AND CONSTRAINT_NAME='fk_producto_proveedor_producto'",sql)) {
                var deleteRule=Convert.ToString(await rule.ExecuteScalarAsync());
                if(deleteRule is "RESTRICT" or "NO ACTION") {
                    await using var alter=new MySqlCommand("ALTER TABLE producto_proveedor DROP FOREIGN KEY fk_producto_proveedor_producto, ADD CONSTRAINT fk_producto_proveedor_producto_cascade FOREIGN KEY(producto_id) REFERENCES producto(id) ON DELETE CASCADE",sql);
                    await alter.ExecuteNonQueryAsync();
                }
            }
            foreach(var item in plan) {
                var p=item.Record;var reference=p["reference"]!.ToString();var sourceId=p["sourceId"]!.ToString();
                long productId=0;bool created=false;string priorState="";
                try {
                    await using(var cmd=new MySqlCommand("SELECT producto_id,creado_por_importacion,estado FROM producto_proveedor WHERE proveedor='Munreco' AND referencia_externa=@source",sql)) {
                        cmd.Parameters.AddWithValue("@source",sourceId);
                        await using var reader=await cmd.ExecuteReaderAsync();
                        if(await reader.ReadAsync()){productId=reader.GetInt64(0);created=reader.GetBoolean(1);priorState=reader.GetString(2);}
                    }
                    await using(var tx=await sql.BeginTransactionAsync()) {
                        if(productId==0) {
                            if(item.ExistingId.HasValue)productId=item.ExistingId.Value;
                            else {
                                long catId;
                                await using(var cat=new MySqlCommand("INSERT INTO categoria(nombre) VALUES(@name) ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id)",sql,tx)) {
                                    cat.Parameters.AddWithValue("@name",item.CategoryName);await cat.ExecuteNonQueryAsync();catId=cat.LastInsertedId;
                                }
                                if(catId<=0)throw new InvalidOperationException("Invalid category id");
                                var preview=p["title"]!.ToString();if(preview.Length>500)preview=preview[..500];
                                await using var insert=new MySqlCommand("INSERT INTO producto(nombre,descripcion,precio,categoria_id,eliminado,fecha_eliminado) VALUES(@name,@description,@price,@category,1,NULL)",sql,tx);
                                insert.Parameters.AddWithValue("@name",reference);insert.Parameters.AddWithValue("@description",preview);
                                insert.Parameters.AddWithValue("@price",p["price"]!.GetValue<decimal>());insert.Parameters.AddWithValue("@category",catId);
                                await insert.ExecuteNonQueryAsync();productId=insert.LastInsertedId;created=true;
                                if(await images.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("producto_id",productId))>0)throw new InvalidOperationException("Allocated product ID already has legacy images; transaction rolled back");
                            }
                        }
                        const string upsert="""
                        INSERT INTO producto_proveedor
                        (proveedor,referencia_externa,referencia,producto_id,marca,seccion,coleccion,descripcion_completa,pvp,moneda,disponibilidad_proveedor,cantidad_proveedor,caracteristicas_json,datos_json,imagenes_json,url_origen,estado,creado_por_importacion,fecha_captura,fecha_importacion)
                        VALUES ('Munreco',@source,@reference,@product,@brand,@section,@collection,@description,@price,'EUR',@availability,@quantity,@attributes,@data,'[]',@url,'pendiente_imagenes',@created,@capture,UTC_TIMESTAMP(6))
                        ON DUPLICATE KEY UPDATE marca=VALUES(marca),seccion=VALUES(seccion),coleccion=VALUES(coleccion),descripcion_completa=VALUES(descripcion_completa),pvp=VALUES(pvp),disponibilidad_proveedor=VALUES(disponibilidad_proveedor),cantidad_proveedor=VALUES(cantidad_proveedor),caracteristicas_json=VALUES(caracteristicas_json),datos_json=VALUES(datos_json),fecha_captura=VALUES(fecha_captura)
                        """;
                        await using var meta=new MySqlCommand(upsert,sql,tx);
                        meta.Parameters.AddWithValue("@source",sourceId);meta.Parameters.AddWithValue("@reference",reference);meta.Parameters.AddWithValue("@product",productId);
                        meta.Parameters.AddWithValue("@brand",p["brand"]!.ToString());meta.Parameters.AddWithValue("@section",p["section"]!.ToString());
                        meta.Parameters.AddWithValue("@collection",p["collection"]?.ToString()??"");meta.Parameters.AddWithValue("@description",p["description"]!.ToString());
                        meta.Parameters.AddWithValue("@price",p["price"]!.GetValue<decimal>());meta.Parameters.AddWithValue("@availability",p["supplierAvailability"]?.ToString()??"");
                        meta.Parameters.AddWithValue("@quantity",int.TryParse(p["supplierQuantity"]?.ToString(),out var qty)?qty:DBNull.Value);
                        meta.Parameters.AddWithValue("@attributes",p["attributeGroups"]?.ToJsonString()??"{}");meta.Parameters.AddWithValue("@data",p.ToJsonString());
                        meta.Parameters.AddWithValue("@url",p["sourceUrl"]!.ToString());meta.Parameters.AddWithValue("@created",created);
                        meta.Parameters.AddWithValue("@capture",DateTime.Parse(p["capturedAt"]!.ToString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal));
                        await meta.ExecuteNonQueryAsync();await tx.CommitAsync();
                    }
                    var current=await images.Find(Builders<BsonDocument>.Filter.Eq("producto_id",productId)).ToListAsync();
                    var byHash=new Dictionary<string,BsonDocument>();
                    foreach(var doc in current)if(doc.TryGetValue("imagen_data",out var data)&&data.IsBsonBinaryData)byHash.TryAdd(Hash(data.AsBsonBinaryData.Bytes),doc);
                    var nextOrder=current.Count>0?current.Max(x=>x["orden"].ToInt32())+1:0;
                    var hasPrincipal=current.Any(x=>x.GetValue("es_principal",false).ToBoolean());
                    var linked=new List<object>();int added=0;
                    foreach(var image in p["images"]!.AsArray()) {
                        var sha=image!["sha256"]!.ToString();
                        if(byHash.TryGetValue(sha,out var old)) {linked.Add(new{mongoId=old["_id"].ToString(),sha256=sha,created=false});continue;}
                        var bytes=await File.ReadAllBytesAsync(ImagePath(o.ImageStore??o.Root,image["file"]!.ToString()));
                        if(Hash(bytes)!=sha)throw new InvalidOperationException("Image changed after validation");
                        var idBytes=SHA256.HashData(Encoding.UTF8.GetBytes("Munreco|"+productId+"|"+sha)).Take(12).ToArray();
                        var mongoId=new ObjectId(idBytes);
                        var doc=new BsonDocument {
                            {"_id",mongoId},{"producto_id",new BsonInt64(productId)},{"imagen_data",new BsonBinaryData(bytes)},
                            {"tipo_mime",image["mimeType"]!.ToString()},{"tamano_bytes",bytes.Length},{"orden",nextOrder++},
                            {"es_principal",!hasPrincipal},{"fecha_creacion",new BsonDateTime(DateTime.UtcNow)},
                            {"nombre_archivo",Uri.UnescapeDataString(Path.GetFileName(new Uri(image["downloadUrl"]!.ToString()).AbsolutePath))}
                        };
                        await images.InsertOneAsync(doc);hasPrincipal=true;added++;byHash[sha]=doc;
                        linked.Add(new{mongoId=mongoId.ToString(),sha256=sha,created=true});
                    }
                    await using(var tx=await sql.BeginTransactionAsync()) {
                        // Activate only a newly created pending product, never a previously completed/deleted one.
                        if(created && priorState!="completo") {
                            await using var activate=new MySqlCommand("UPDATE producto SET eliminado=@hidden,fecha_eliminado=IF(@hidden,UTC_TIMESTAMP(),NULL) WHERE id=@id",sql,tx);
                            activate.Parameters.AddWithValue("@id",productId);
                            activate.Parameters.AddWithValue("@hidden",p["requiresReview"]?.GetValue<bool>()==true || !int.TryParse(p["supplierQuantity"]?.ToString(),out var availableQty) || availableQty<=0);
                            await activate.ExecuteNonQueryAsync();
                        }
                        await using var finish=new MySqlCommand("UPDATE producto_proveedor SET estado='completo',imagenes_json=@images,fecha_importacion=UTC_TIMESTAMP(6) WHERE proveedor='Munreco' AND referencia_externa=@source",sql,tx);
                        finish.Parameters.AddWithValue("@images",JsonSerializer.Serialize(linked));finish.Parameters.AddWithValue("@source",sourceId);await finish.ExecuteNonQueryAsync();await tx.CommitAsync();
                    }
                    results.Add(new{reference,sourceId,productId,created,imagesAdded=added,status="completo"});
                    if(results.Count%25==0 || plan.Count<=10)Console.WriteLine($"IMPORTED {results.Count}/{plan.Count} {reference} mysqlId={productId} imagesAdded={added}");
                }catch(Exception e) {
                    results.Add(new{reference,sourceId,productId,status="error",error=e.GetType().Name});
                    await File.WriteAllTextAsync(Path.Combine(o.Root,"importacion-informe.json"),JsonSerializer.Serialize(results,Json));
                    throw new InvalidOperationException("Import interrupted at "+reference+"; pending records can be resumed",e);
                }
                if(results.Count%25==0)await File.WriteAllTextAsync(Path.Combine(o.Root,"importacion-informe.json"),JsonSerializer.Serialize(results,Json));
            }
            await File.WriteAllTextAsync(Path.Combine(o.Root,o.Limit>0?"importacion-muestra.json":"importacion-informe.json"),JsonSerializer.Serialize(results,Json));
            Console.WriteLine("IMPORT_FINISHED "+results.Count);
        }finally{await using var release=new MySqlCommand("SELECT RELEASE_LOCK('alda_munreco_import')",sql);await release.ExecuteScalarAsync();}
    }
    static async Task Verify(MySqlConnection sql,IMongoCollection<BsonDocument> images,ImportOptions o) {
        var rows=new List<(long Id,string Ref,bool Created,string State,int Expected)>();
        await using(var cmd=new MySqlCommand("SELECT producto_id,referencia,creado_por_importacion,estado,JSON_LENGTH(imagenes_json) FROM producto_proveedor WHERE proveedor='Munreco'",sql)) {
            await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add((r.GetInt64(0),r.GetString(1),r.GetBoolean(2),r.GetString(3),r.GetInt32(4)));
        }
        var errors=new List<string>();long imageTotal=0;
        foreach(var row in rows) {
            var docs=await images.Find(Builders<BsonDocument>.Filter.Eq("producto_id",row.Id)).Project(Builders<BsonDocument>.Projection.Exclude("imagen_data")).ToListAsync();
            imageTotal+=docs.Count;
            if(row.State!="completo")errors.Add(row.Ref+":pending");
            if(docs.Count<row.Expected || docs.Count==0 || (row.Created && docs.Count!=row.Expected))errors.Add(row.Ref+":missing-images");
            if(docs.Select(x=>x["orden"].ToInt32()).Distinct().Count()!=docs.Count)errors.Add(row.Ref+":duplicate-order");
            if(row.Created && docs.Count(x=>x["es_principal"].ToBoolean())!=1)errors.Add(row.Ref+":principal");
        }
        long total;
        await using(var count=new MySqlCommand("SELECT COUNT(*) FROM producto",sql))total=Convert.ToInt64(await count.ExecuteScalarAsync());
        var result=new{verifiedAt=DateTime.UtcNow,supplierProducts=rows.Count,newProducts=rows.Count(x=>x.Created),existingProducts=rows.Count(x=>!x.Created),totalMysqlProducts=total,linkedMongoImages=imageTotal,errors};
        await File.WriteAllTextAsync(Path.Combine(o.Root,"verificacion.json"),JsonSerializer.Serialize(result,Json));
        Console.WriteLine(JsonSerializer.Serialize(result,Json));if(errors.Count>0)throw new InvalidOperationException("Verification failed");
    }
}
