using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;

namespace PerezMoraSync;

public static class Engine {
    public const string Supplier="PerezMora";
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    static string Serialize(object value)=>JsonSerializer.Serialize(value,Json);
    static async Task<bool> Table(MySqlConnection sql,string name) {
        await using var cmd=new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@name",sql);
        cmd.Parameters.AddWithValue("@name",name);return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
    }
    public static async Task Run(CatalogData catalog,string root,string cache,bool apply,bool offlineImages=false) {
        using var config=JsonDocument.Parse(Environment.GetEnvironmentVariable("ALDA_PEREZMORA_CONNECTIONS")??throw new ArgumentException("Falta la configuración privada de las bases de datos."));
        var sqlConfig=config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        await using var sql=new MySqlConnection(new MySqlConnectionStringBuilder(sqlConfig!){ConnectionTimeout=15,DefaultCommandTimeout=60,Pooling=false}.ConnectionString);
        await sql.OpenAsync();
        var lockName="alda_perez_mora_import_"+Catalog.Hash(sql.Database)[..16];
        await using(var mutex=new MySqlCommand("SELECT GET_LOCK(@key,0)",sql)) {
            mutex.Parameters.AddWithValue("@key",lockName);
            if(Convert.ToInt32(await mutex.ExecuteScalarAsync())!=1)throw new InvalidDataException("Ya existe otra actualización de Pérez Mora en marcha.");
        }
        try {
            decimal coefficient=1;
            if(await Table(sql,"perez_mora_pricing")) {
                await using var pricing=new MySqlCommand("SELECT coeficiente FROM perez_mora_pricing WHERE id=1",sql);
                var value=await pricing.ExecuteScalarAsync();if(value!=null&&value!=DBNull.Value)coefficient=Convert.ToDecimal(value);
            }
            catalog = catalog with { Products = catalog.Products.Select(p => p with {
                Category = p.Category.Length == 0 ? "" : AldaJoyeros.Catalog.CatalogTaxonomy.Specific(p.Category, p.Description,
                    p.Fields.GetValueOrDefault("METAL", ""), p.Fields.GetValueOrDefault("TIPO", ""))
            }).ToArray() };
            foreach(var product in catalog.Products)AldaJoyeros.Catalog.ProviderPricing.Pvp(product.Price,coefficient);
            var existing=new List<CurrentProduct>();
            await using(var cmd=new MySqlCommand("SELECT id,nombre,descripcion,precio,categoria_id,eliminado,fecha_eliminado FROM producto",sql)) {
                await using var reader=await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync())existing.Add(new(reader.GetInt64(0),reader.GetString(1),reader.IsDBNull(2)?"":reader.GetString(2),Convert.ToDecimal(reader.GetValue(3)),reader.IsDBNull(4)?null:reader.GetInt64(4),reader.GetBoolean(5),reader.IsDBNull(6)?null:reader.GetDateTime(6)));
            }
            var byId=existing.ToDictionary(x=>x.Id);var byRef=existing.GroupBy(x=>x.Reference,StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.ToArray(),StringComparer.OrdinalIgnoreCase);
            var previous=new Dictionary<string,ManagedProduct>(StringComparer.OrdinalIgnoreCase);
            if(await Table(sql,"perez_mora_product_sync")) {
                await using var cmd=new MySqlCommand("SELECT estado_json FROM perez_mora_product_sync",sql);await using var reader=await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync()){var state=JsonSerializer.Deserialize<ManagedProduct>(reader.GetString(0),Json)!;if(byId.ContainsKey(state.ProductId))previous.Add(state.Reference,state);}
            }
            // Recover an interrupted first import from its provider mapping even before the first state was committed.
            if(await Table(sql,"producto_proveedor")) {
                await using var cmd=new MySqlCommand("SELECT referencia,producto_id,creado_por_importacion FROM producto_proveedor WHERE proveedor=@supplier",sql);cmd.Parameters.AddWithValue("@supplier",Supplier);
                await using var reader=await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync()) {
                    var reference=reader.GetString(0);var id=reader.GetInt64(1);
                    if(previous.ContainsKey(reference)||!byId.TryGetValue(id,out var product))continue;
                    previous.Add(reference,new(id,reference,"",0,true,product.Hidden,product.DeletedAt,product.Price,product.Description,product.Category??0,false,false,false,false,[],reader.GetBoolean(2)));
                }
            }
            var present=catalog.Products.Select(x=>x.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed=previous.Values.Where(x=>x.Present&&!present.Contains(x.Reference)).ToArray();
            var collisions=catalog.Products.Where(x=>!previous.ContainsKey(x.Reference)&&byRef.ContainsKey(x.Reference)).Select(x=>x.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var accepted=catalog.Products.Where(x=>!collisions.Contains(x.Reference)).ToArray();
            var changes=accepted.Select(x=>new{reference=x.Reference,action=!previous.TryGetValue(x.Reference,out var old)?"nuevo":
                !old.Present||x.Stock>old.Stock?"reposicion":old.Stock>0&&x.Stock==0?"agotado":old.Fingerprint!=x.Fingerprint||(!old.ManualPrice&&old.LastPrice!=AldaJoyeros.Catalog.ProviderPricing.Pvp(x.Price,coefficient))?"modificado":"sin_cambios",stock=x.Stock,review=x.RequiresReview}).ToArray();
            var summary=new {products=catalog.Products.Count,added=changes.Count(x=>x.action=="nuevo"),restocked=changes.Count(x=>x.action=="reposicion"),
                soldOut=changes.Count(x=>x.action=="agotado"),modified=changes.Count(x=>x.action=="modificado"),removed=removed.Length,
                needsReview=catalog.Products.Count(x=>x.RequiresReview),collisions=collisions.Count,imageLinks=catalog.Products.Sum(x=>x.ImageUrls.Length),fileHash=catalog.FileHash};
            await File.WriteAllTextAsync(Path.Combine(root,"comparacion.json"),Serialize(new{summary.products,summary.added,summary.restocked,summary.soldOut,summary.modified,summary.removed,summary.needsReview,summary.collisions,summary.imageLinks,summary.fileHash,
                applied=false,status="comparacion",coefficient,changes,retired=removed.Select(x=>x.Reference),conflictingReferences=collisions.Order().ToArray(),catalogSummary=catalog.Summary}));
            Console.WriteLine($"CATALOG {catalog.Products.Count} new={summary.added} collisions={collisions.Count}");
            if(!apply)return;
            Catalog.ValidateRemovals(catalog.Products,previous.Values.Where(x=>x.Present).Select(x=>x.Reference));
            await File.WriteAllTextAsync(Path.Combine(root,"respaldo-catalogo.json"),Serialize(new{createdAt=DateTime.UtcNow,products=existing,managed=previous.Values}));
            var mongoConfig=config.RootElement.GetProperty("MongoDbSettings");
            var mongoSettings=MongoClientSettings.FromConnectionString(mongoConfig.GetProperty("ConnectionString").GetString()!);mongoSettings.ServerSelectionTimeout=TimeSpan.FromSeconds(20);
            var db=new MongoClient(mongoSettings).GetDatabase(mongoConfig.GetProperty("DatabaseName").GetString());
            await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping",1));var images=db.GetCollection<BsonDocument>("ProductoImagenes");
            // All images are downloaded and validated before changing any product or applying retirements.
            var imageCache=new ImageCache(cache,offlineImages);
            var downloaded=await imageCache.Download(accepted.SelectMany(x=>x.ImageUrls));
            await File.WriteAllTextAsync(Path.Combine(root,"imagenes-incidencias.json"),Serialize(imageCache.Failures.Keys.Order().ToArray()));
            await File.WriteAllTextAsync(Path.Combine(root,"revision.json"),Serialize(catalog.Review(imageCache.Failures.Keys)));
            await Schema.Ensure(sql);int count=0;
            foreach(var original in accepted) {
                var product=original with {ImageFailure=original.ImageUrls.Any(x=>!downloaded.ContainsKey(x))};
                previous.TryGetValue(product.Reference,out var old);
                var id=old?.ProductId??0;
                if(id==0) {
                    await using var tx=await sql.BeginTransactionAsync();var category=await Category(sql,tx,product.Category.Length>0?product.Category:"Pérez Mora · Pendientes de revisar");
                    await using(var insert=new MySqlCommand("INSERT INTO producto(nombre,descripcion,precio,categoria_id,eliminado,fecha_eliminado) VALUES(@ref,@description,@price,@category,1,NULL)",sql,tx)) {
                        insert.Parameters.AddWithValue("@ref",product.Reference);insert.Parameters.AddWithValue("@description",product.Description);insert.Parameters.AddWithValue("@price",AldaJoyeros.Catalog.ProviderPricing.Pvp(product.Price,coefficient));insert.Parameters.AddWithValue("@category",category);
                        await insert.ExecuteNonQueryAsync();id=insert.LastInsertedId;
                    }
                    if(await images.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("producto_id",id))>0)
                        throw new InvalidDataException("El identificador nuevo ya tiene imágenes antiguas. Revisar la correspondencia entre MySQL y MongoDB antes de continuar.");
                    old=new(id,product.Reference,"",0,true,true,null,AldaJoyeros.Catalog.ProviderPricing.Pvp(product.Price,coefficient),product.Description,category,false,false,false,false,[],true);
                    await Metadata(sql,tx,product,id,true,[],"pendiente_imagenes");await State(sql,tx,old);await tx.CommitAsync();
                    byId[id]=new(id,product.Reference,product.Description,product.Price,category,true,null);
                }
                // Reload immediately before applying policy so changes made in the admin panel during downloads are respected.
                var current=await Current(sql,id);var decision=Policy.Decide(old!,current,product);
                var selected=await ImageCache.Store(images,id,product.ImageUrls.Where(downloaded.ContainsKey).Select(x=>downloaded[x]),old!.Images);
                if(product.ImageFailure)selected=selected.Union(old.Images).ToArray();
                await using(var tx=await sql.BeginTransactionAsync()) {
                    var category=decision.ManualCategory?current.Category??old.LastCategory:await Category(sql,tx,product.Category.Length>0?product.Category:"Pérez Mora · Pendientes de revisar");
                    var price=decision.ManualPrice?current.Price:AldaJoyeros.Catalog.ProviderPricing.Pvp(product.Price,coefficient);var description=decision.ManualDescription?current.Description:product.Description;
                    // Keep the same timestamp while hidden; a changed timestamp is a manual visibility decision.
                    var deleted=decision.Hidden?(current.Hidden?current.DeletedAt:DateTime.UtcNow):null;
                    await using(var update=new MySqlCommand("UPDATE producto SET descripcion=@description,precio=@price,categoria_id=@category,eliminado=@hidden,fecha_eliminado=@deleted WHERE id=@id",sql,tx)) {
                        update.Parameters.AddWithValue("@description",description);update.Parameters.AddWithValue("@price",price);update.Parameters.AddWithValue("@category",category);update.Parameters.AddWithValue("@hidden",decision.Hidden);update.Parameters.AddWithValue("@deleted",(object?)deleted??DBNull.Value);update.Parameters.AddWithValue("@id",id);await update.ExecuteNonQueryAsync();
                    }
                    // Use DB precision for the managed deletion timestamp.
                    await using(var stamp=new MySqlCommand("SELECT fecha_eliminado FROM producto WHERE id=@id",sql,tx)){stamp.Parameters.AddWithValue("@id",id);var value=await stamp.ExecuteScalarAsync();deleted=value==null||value==DBNull.Value?null:Convert.ToDateTime(value);}
                    var state=new ManagedProduct(id,product.Reference,product.Fingerprint,product.Stock,true,decision.Hidden,deleted,price,description,category,
                        decision.ManualVisibility,decision.ManualPrice,decision.ManualDescription,decision.ManualCategory,selected,old.Created);
                    await Metadata(sql,tx,product,id,old.Created,selected,product.RequiresReview?"revision":"completo");await State(sql,tx,state);await tx.CommitAsync();
                }
                if(!product.ImageFailure)await ImageCache.RemoveOld(images,id,old.Images,selected);
                if(++count%100==0||count==accepted.Length)Console.WriteLine($"IMPORTED {count}/{accepted.Length}");
            }
            // Retire only this provider's automatically managed products after the entire current catalog succeeded.
            foreach(var old in removed) {
                var current=await Current(sql,old.ProductId);var decision=Policy.Decide(old,current,null);
                await using var tx=await sql.BeginTransactionAsync();
                DateTime? deleted=current.DeletedAt;
                if(!decision.ManualVisibility&&!current.Hidden) {
                    await using var hide=new MySqlCommand("UPDATE producto SET eliminado=1,fecha_eliminado=UTC_TIMESTAMP() WHERE id=@id",sql,tx);hide.Parameters.AddWithValue("@id",old.ProductId);await hide.ExecuteNonQueryAsync();
                    await using var stamp=new MySqlCommand("SELECT fecha_eliminado FROM producto WHERE id=@id",sql,tx);stamp.Parameters.AddWithValue("@id",old.ProductId);deleted=Convert.ToDateTime(await stamp.ExecuteScalarAsync());
                }
                await State(sql,tx,old with{Present=false,Hidden=decision.Hidden,DeletedAt=deleted,ManualVisibility=decision.ManualVisibility});
                await using var meta=new MySqlCommand("UPDATE producto_proveedor SET estado='retirado',disponibilidad_proveedor='Retirado del catálogo' WHERE proveedor=@supplier AND referencia=@ref",sql,tx);
                meta.Parameters.AddWithValue("@supplier",Supplier);meta.Parameters.AddWithValue("@ref",old.Reference);await meta.ExecuteNonQueryAsync();await tx.CommitAsync();
            }
            // Verify product and image identities after applying the import.
            var verified=0;var imageCount=0;
            await using(var verify=new MySqlCommand("SELECT s.estado_json FROM perez_mora_product_sync s JOIN producto_proveedor pp ON pp.proveedor=@supplier AND pp.referencia=s.referencia AND pp.producto_id=s.producto_id",sql)) {
                verify.Parameters.AddWithValue("@supplier",Supplier);await using var reader=await verify.ExecuteReaderAsync();var states=new List<ManagedProduct>();
                while(await reader.ReadAsync())states.Add(JsonSerializer.Deserialize<ManagedProduct>(reader.GetString(0),Json)!);
                await reader.DisposeAsync();
                foreach(var state in states.Where(x=>present.Contains(x.Reference))) {
                    if(state.Images.Length>0) {
                        var filter=Builders<BsonDocument>.Filter.Eq("producto_id",state.ProductId)&Builders<BsonDocument>.Filter.In("_id",state.Images.Select(ObjectId.Parse));
                        var actual=await images.CountDocumentsAsync(filter);if(actual!=state.Images.Length)throw new InvalidDataException("Faltan imágenes tras la importación: "+state.Reference);imageCount+=state.Images.Length;
                    }
                    verified++;
                }
            }
            if(verified!=accepted.Length)throw new InvalidDataException("No coinciden los productos verificados con el catálogo procesado.");
            await File.WriteAllTextAsync(Path.Combine(root,"resultado.json"),Serialize(new{summary.products,summary.added,summary.restocked,summary.soldOut,summary.modified,summary.removed,
                needsReview=accepted.Count(x=>x.RequiresReview||x.ImageUrls.Any(u=>!downloaded.ContainsKey(u))),summary.collisions,summary.fileHash,
                applied=true,status="completo",coefficient,verifiedProducts=verified,linkedImages=imageCount,imageDownloadFailures=imageCache.Failures.Count,errors=Array.Empty<string>()}));
            Console.WriteLine($"VERIFIED {verified} images={imageCount}");
        }finally{await using var release=new MySqlCommand("SELECT RELEASE_LOCK(@key)",sql);release.Parameters.AddWithValue("@key",lockName);await release.ExecuteScalarAsync();}
    }
    static async Task<CurrentProduct> Current(MySqlConnection sql,long id) {
        await using var cmd=new MySqlCommand("SELECT nombre,descripcion,precio,categoria_id,eliminado,fecha_eliminado FROM producto WHERE id=@id",sql);cmd.Parameters.AddWithValue("@id",id);
        await using var reader=await cmd.ExecuteReaderAsync();if(!await reader.ReadAsync())throw new InvalidDataException("Se ha eliminado un producto durante la actualización.");
        return new(id,reader.GetString(0),reader.IsDBNull(1)?"":reader.GetString(1),Convert.ToDecimal(reader.GetValue(2)),reader.IsDBNull(3)?null:reader.GetInt64(3),reader.GetBoolean(4),reader.IsDBNull(5)?null:reader.GetDateTime(5));
    }
    static async Task<long> Category(MySqlConnection sql,MySqlTransaction tx,string name) {
        name=AldaJoyeros.Catalog.CatalogTaxonomy.Normalize(name);
        var family = AldaJoyeros.Catalog.CatalogTaxonomy.Family(name);
        if (family != name)
        {
            await using var parent = new MySqlCommand("INSERT INTO categoria(nombre) VALUES(@name) ON DUPLICATE KEY UPDATE nombre=VALUES(nombre)",sql,tx);
            parent.Parameters.AddWithValue("@name",family); await parent.ExecuteNonQueryAsync();
        }
        await using var cmd=new MySqlCommand("INSERT INTO categoria(nombre) VALUES(@name) ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id)",sql,tx);cmd.Parameters.AddWithValue("@name",name);await cmd.ExecuteNonQueryAsync();return cmd.LastInsertedId;
    }
    static async Task State(MySqlConnection sql,MySqlTransaction tx,ManagedProduct state) {
        await using var cmd=new MySqlCommand("INSERT INTO perez_mora_product_sync VALUES(@ref,@id,@state,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE producto_id=VALUES(producto_id),estado_json=VALUES(estado_json),actualizado_utc=VALUES(actualizado_utc)",sql,tx);
        cmd.Parameters.AddWithValue("@ref",state.Reference);cmd.Parameters.AddWithValue("@id",state.ProductId);cmd.Parameters.AddWithValue("@state",Serialize(state));await cmd.ExecuteNonQueryAsync();
    }
    static async Task Metadata(MySqlConnection sql,MySqlTransaction tx,CatalogProduct product,long id,bool created,string[] images,string state) {
        const string query="""
        INSERT INTO producto_proveedor
        (proveedor,referencia_externa,referencia,producto_id,marca,seccion,coleccion,descripcion_completa,pvp,moneda,disponibilidad_proveedor,cantidad_proveedor,caracteristicas_json,datos_json,imagenes_json,url_origen,estado,creado_por_importacion,fecha_captura,fecha_importacion)
        VALUES (@supplier,@source,@ref,@id,'Pérez Mora',@category,@subcategory,@description,@price,'EUR',@availability,@stock,@attributes,@data,@images,'https://joseperezmora.es/descargas.php',@state,@created,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE seccion=VALUES(seccion),coleccion=VALUES(coleccion),descripcion_completa=VALUES(descripcion_completa),pvp=VALUES(pvp),disponibilidad_proveedor=VALUES(disponibilidad_proveedor),cantidad_proveedor=VALUES(cantidad_proveedor),caracteristicas_json=VALUES(caracteristicas_json),datos_json=VALUES(datos_json),imagenes_json=VALUES(imagenes_json),estado=VALUES(estado),fecha_captura=VALUES(fecha_captura),fecha_importacion=VALUES(fecha_importacion)
        """;
        await using var cmd=new MySqlCommand(query,sql,tx);
        var attributes=product.Fields.Where(x=>!x.Key.StartsWith("IMAGEN ")&&x.Key is not ("REFERENCIA" or "DESCRIPCION" or "PRECIO" or "STOCK" or "CATEGORIA" or "SUBCATEGORIA")).ToDictionary(x=>x.Key,x=>x.Value);
        // Match the grouped characteristic structure expected by the storefront.
        foreach(var pair in new Dictionary<string,object>{{"@supplier",Supplier},{"@source",Catalog.Hash(product.Reference.ToUpperInvariant())},{"@ref",product.Reference},{"@id",id},{"@category",product.Category},{"@subcategory",product.Subcategory},
            {"@description",product.Description},{"@price",product.Price},{"@stock",product.Stock},{"@availability",product.Stock>0?"Disponible":"Sin existencias"},
            {"@attributes",Serialize(new[]{new{name="Características",attribute=attributes.Where(x=>x.Value.Length>0).Select(x=>new{name=x.Key,text=x.Value}).ToArray()}})},
            {"@data",Serialize(product.Fields)},{"@images",Serialize(images)},{"@state",state},{"@created",created}})cmd.Parameters.AddWithValue(pair.Key,pair.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
