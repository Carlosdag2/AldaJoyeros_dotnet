using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using MySqlConnector;

record SupplierRow(string Source,string Reference,long Product,bool Created,bool Hidden,DateTime? DeletedAt,int? Quantity,DateTime Imported,JsonObject Data,ManagedState? Managed);

static partial class MonthlySync {
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    static async Task Save(string path,object value) {
        var temporary=path+".tmp";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(value,Json));File.Move(temporary,path,true);
    }
    public static async Task Run(string[] args) {
        string Value(string key,string fallback){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        var apply=args.Contains("--apply");var offline=args.Contains("--offline-plan");
        if(apply&&offline)throw new InvalidOperationException("Los datos antiguos solo permiten simulacion, nunca aplicar cambios");
        var config=Path.GetFullPath(Value("--config","appsettings.json"));
        var auth=Path.GetFullPath(Value("--auth","work/munreco-session/auth.json"));
        var imageStore=Path.GetFullPath(Value("--image-store","outputs/munreco"));
        var history=Path.GetFullPath(Value("--history","outputs/munreco-mensual"));
        var privateHistory=Path.GetFullPath(Value("--private-history","work/munreco-mensual"));
        var runId=Value("--run-id",DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")+"-"+Guid.NewGuid().ToString("N")[..6]);
        if(!System.Text.RegularExpressions.Regex.IsMatch(runId,@"\A[a-zA-Z0-9-]{1,80}\z"))throw new InvalidOperationException("Invalid run id");
        var output=Path.Combine(history,runId);var session=Path.Combine(privateHistory,runId);
        Directory.CreateDirectory(output);Directory.CreateDirectory(session);
        Directory.CreateDirectory(Path.Combine(output,"productos"));
        using var settings=await RuntimeConfiguration.Read(config);
        var cs=settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        await using var sql=new MySqlConnection(cs);await sql.OpenAsync();
        await using(var getLock=new MySqlCommand("SELECT GET_LOCK('alda_munreco_monthly',0)",sql)) {
            if(Convert.ToInt32(await getLock.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Ya hay una sincronizacion mensual en marcha");
        }
        try {
            var previous=await ReadRows(sql);
            if(previous.Count==0)throw new InvalidOperationException("No existe la importacion inicial de Munreco en esta base de datos");
            using var pw=await Playwright.CreateAsync();
            if(offline) {
                File.Copy(Path.Combine(imageStore,"cobertura.json"),Path.Combine(session,"coverage.json"));
                var historical=new JsonArray();
                foreach(var f in Directory.GetFiles(Path.Combine(imageStore,"productos"),"*.json"))historical.Add(JsonNode.Parse(await File.ReadAllTextAsync(f))!["sourceData"]!.DeepClone());
                await Save(Path.Combine(session,"all-products-expanded.json"),historical);
                Console.WriteLine("SIMULACION con datos historicos: prohibido aplicar");
            } else {
                if(!File.Exists(auth))throw new InvalidOperationException("Falta la sesion B2B. Inicia sesion antes de sincronizar");
                File.Copy(auth,Path.Combine(session,"auth.json"));
                Console.WriteLine("Consultando catalogo nuevo; no se reutilizan paginas de ejecuciones anteriores");
                await CatalogCrawler.Run(pw,session);
            }
            var catalog=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(session,"all-products-expanded.json")))!.AsArray();
            var coverage=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(session,"coverage.json")))!.AsObject();
            // Compare with the last successfully observed section, excluding already withdrawn products.
            SyncPolicy.ValidateCoverage(catalog,coverage,previous.Where(x=>x.Managed?.Present!=false).Select(x=>(x.Source,x.Data["sectionId"]!.ToString())));
            var rawById=catalog.Select(x=>x!.AsObject()).ToDictionary(x=>x["product_id"]!.ToString());
            var oldById=previous.ToDictionary(x=>x.Source);
            var oldByRef=previous.ToDictionary(x=>x.Reference,StringComparer.OrdinalIgnoreCase);
            foreach(var raw in rawById.Values) {
                var id=raw["product_id"]!.ToString();var reference=raw["model"]!.ToString();
                if(oldById.TryGetValue(id,out var old)&&old.Reference!=reference || oldByRef.TryGetValue(reference,out var prior)&&prior.Source!=id)
                    throw new InvalidOperationException("Ha cambiado la identidad de una referencia: "+reference+"; requiere revision");
            }
            File.Copy(Path.Combine(session,"coverage.json"),Path.Combine(output,"cobertura.json"));
            var changes=new List<object>();int added=0,removed=0,restocked=0,soldOut=0,modified=0;
            foreach(var raw in rawById.Values) {
                var id=raw["product_id"]!.ToString();var reference=raw["model"]!.ToString();
                if(!oldById.TryGetValue(id,out var old)){added++;changes.Add(new{reference,type="nuevo",quantity=SyncPolicy.Quantity(raw)});continue;}
                var quantity=SyncPolicy.Quantity(raw);
                var wasRemoved=old.Managed?.Present==false;
                if(quantity>0 && (old.Quantity<=0 || wasRemoved)){restocked++;changes.Add(new{reference,type="repuesto",quantity});}
                else if(quantity==0 && old.Quantity>0){soldOut++;changes.Add(new{reference,type="agotado",quantity});}
                if(old.Data["sourceData"] is JsonObject before && !JsonNode.DeepEquals(SyncPolicy.DetailComparable(before),SyncPolicy.DetailComparable(raw))) {
                    modified++;changes.Add(new{reference,type="datos_o_pvp_modificados",previousPvp=old.Data["price"]?.ToString(),currentPriceText=raw["price"]?.ToString()});
                }
            }
            foreach(var old in previous.Where(x=>!rawById.ContainsKey(x.Source) && x.Managed?.Present!=false)) {
                removed++;changes.Add(new{reference=old.Reference,type="retirado",visibilityManaged=old.Created});
            }
            await Save(Path.Combine(output,"comparacion.json"),new{runId,checkedAt=DateTime.UtcNow,apply,offlineSimulation=offline,products=catalog.Count,added,removed,restocked,soldOut,modified,changes});
            Console.WriteLine($"COMPARACION nuevos={added} retirados={removed} reposiciones={restocked} agotados={soldOut} cambios={modified}");
            if(!apply) {Console.WriteLine("PLAN_ONLY: "+output);return;}
            // Enrol before updating provider metadata so interrupted runs retain the previous managed state.
            await EnsureStates(sql,previous,InitialVerificationTime(imageStore));
            await Save(Path.Combine(output,"estado-antes.json"),previous);
            int reused=0;
            foreach(var raw in rawById.Values) {
                var id=raw["product_id"]!.ToString();
                if(args.Contains("--full") || !oldById.TryGetValue(id,out var old) || old.Data["sourceData"] is not JsonObject source ||
                    !JsonNode.DeepEquals(SyncPolicy.DetailComparable(source),SyncPolicy.DetailComparable(raw)) || old.Data["requiresReview"]?.GetValue<bool>()==true)continue;
                if(old.Data["images"]!.AsArray().Any(x=>!File.Exists(Path.Combine(imageStore,x!["file"]!.ToString()))))continue;
                var record=old.Data.DeepClone().AsObject();
                record["sourceData"]=raw.DeepClone();record["supplierQuantity"]=raw["quantity"]!.ToString();
                record["supplierAvailability"]=raw["availability"]?.ToString();record["supplierStockStatus"]=raw["stock_status"]?.ToString();
                record["detailsCapturedAt"]=record["detailsCapturedAt"]?.ToString()??record["capturedAt"]?.ToString();
                record["capturedAt"]=DateTime.UtcNow;record["stockCapturedAt"]=DateTime.UtcNow;
                await Save(Path.Combine(output,"productos",id+".json"),record);reused++;
            }
            Console.WriteLine($"Fichas sin cambios reutilizadas: {reused}; revisar detalles: {catalog.Count-reused}");
            await DetailCrawler.Run(pw,session,false,output,imageStore);
            if(DateTime.UtcNow-DateTime.Parse(coverage["completedAt"]!.ToString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal)>TimeSpan.FromHours(24))
                throw new InvalidOperationException("La consulta tiene mas de 24 horas; hay que volver a leer el catalogo");
            var records=Directory.GetFiles(Path.Combine(output,"productos"),"*.json").Select(f=>JsonNode.Parse(File.ReadAllText(f))!.AsObject()).ToDictionary(x=>x["sourceId"]!.ToString());
            if(records.Count!=rawById.Count || records.Keys.Any(x=>!rawById.ContainsKey(x)))throw new InvalidOperationException("Extraccion incompleta; no se importa");
            await ImportRunner.Run(new(config,output,true,false,0,imageStore));
            var visibility=await ApplyVisibility(sql,records);
            await SyncVerification.Run(config,output);
            await Save(Path.Combine(output,"resultado.json"),new{runId,status="completo",completedAt=DateTime.UtcNow,products=records.Count,added,removed,restocked,soldOut,modified,visibility,existingPricesPreserved=true});
            Console.WriteLine("SINCRONIZACION_COMPLETA: "+output);
        } catch(Exception e) {
            await Save(Path.Combine(output,"error.json"),new{runId,status="detenido",at=DateTime.UtcNow,errorType=e.GetType().Name});
            throw;
        } finally {
            await using var release=new MySqlCommand("SELECT RELEASE_LOCK('alda_munreco_monthly')",sql);await release.ExecuteScalarAsync();
        }
    }
    static async Task<List<SupplierRow>> ReadRows(MySqlConnection sql) {
        bool states;
        await using(var c=new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='producto_proveedor_sync'",sql))states=Convert.ToInt32(await c.ExecuteScalarAsync())>0;
        bool presence=false;
        if(states)await using(var c=new MySqlCommand("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='producto_proveedor_sync' AND COLUMN_NAME='presente_proveedor'",sql))presence=Convert.ToInt32(await c.ExecuteScalarAsync())>0;
        var query="SELECT s.referencia_externa,s.referencia,s.producto_id,s.creado_por_importacion,p.eliminado,p.fecha_eliminado,s.cantidad_proveedor,s.fecha_importacion,s.datos_json"+
            (states?",y.oculto,y.fecha_oculto,y.manual,y.motivo,"+(presence?"y.presente_proveedor":"CASE WHEN y.motivo='retirado_proveedor' THEN 0 ELSE 1 END"):",NULL,NULL,NULL,NULL,NULL")+
            " FROM producto_proveedor s JOIN producto p ON p.id=s.producto_id"+
            (states?" LEFT JOIN producto_proveedor_sync y ON y.proveedor=s.proveedor AND y.referencia_externa=s.referencia_externa":"")+" WHERE s.proveedor='Munreco'";
        await using var cmd=new MySqlCommand(query,sql);await using var r=await cmd.ExecuteReaderAsync();var rows=new List<SupplierRow>();
        while(await r.ReadAsync())rows.Add(new(r.GetString(0),r.GetString(1),r.GetInt64(2),r.GetBoolean(3),r.GetBoolean(4),r.IsDBNull(5)?null:r.GetDateTime(5),r.IsDBNull(6)?null:r.GetInt32(6),r.GetDateTime(7),JsonNode.Parse(r.GetString(8))!.AsObject(),r.IsDBNull(9)?null:new(r.GetBoolean(9),r.IsDBNull(10)?null:r.GetDateTime(10),r.GetBoolean(11),r.GetString(12),r.GetBoolean(13))));
        return rows;
    }
    static DateTime? InitialVerificationTime(string imageStore) {
        var path=Path.Combine(imageStore,"verificacion.json");if(!File.Exists(path))return null;
        var report=JsonNode.Parse(File.ReadAllText(path))!;
        if(report["errors"]?.AsArray().Count!=0 || report["newProductsHidden"]?.GetValue<int>()!=report["newWithoutSupplierStock"]?.GetValue<int>())return null;
        return DateTime.Parse(report["verifiedAt"]!.ToString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal);
    }
    static ManagedState Initial(SupplierRow row,DateTime? verifiedAt=null)=>SyncPolicy.Bootstrap(row.Created,row.Hidden,row.DeletedAt,row.Quantity,row.Data["requiresReview"]?.GetValue<bool>()==true,row.Imported,verifiedAt);
    static async Task EnsureStates(MySqlConnection sql,List<SupplierRow> rows,DateTime? verifiedAt=null) {
        const string ddl="""
        CREATE TABLE IF NOT EXISTS producto_proveedor_sync (
          proveedor varchar(32) NOT NULL, referencia_externa varchar(64) NOT NULL,
          oculto tinyint(1) NOT NULL, fecha_oculto datetime NULL, manual tinyint(1) NOT NULL,
          motivo varchar(40) NOT NULL, fecha_comprobacion datetime(6) NOT NULL,
          presente_proveedor tinyint(1) NOT NULL DEFAULT 1,
          PRIMARY KEY(proveedor,referencia_externa),
          FOREIGN KEY(proveedor,referencia_externa) REFERENCES producto_proveedor(proveedor,referencia_externa) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
        """;
        await using(var c=new MySqlCommand(ddl,sql))await c.ExecuteNonQueryAsync();
        await using(var c=new MySqlCommand("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='producto_proveedor_sync' AND COLUMN_NAME='presente_proveedor'",sql)) {
            if(Convert.ToInt32(await c.ExecuteScalarAsync())==0) {
                await using(var alter=new MySqlCommand("ALTER TABLE producto_proveedor_sync ADD presente_proveedor tinyint(1) NOT NULL DEFAULT 1",sql))await alter.ExecuteNonQueryAsync();
                await using var seed=new MySqlCommand("UPDATE producto_proveedor_sync SET presente_proveedor=0 WHERE motivo='retirado_proveedor'",sql);await seed.ExecuteNonQueryAsync();
            }
        }
        foreach(var row in rows.Where(x=>x.Managed==null)) {
            var state=Initial(row,verifiedAt);
            await using var c=new MySqlCommand("INSERT IGNORE INTO producto_proveedor_sync VALUES('Munreco',@source,@hidden,@date,@manual,@reason,UTC_TIMESTAMP(6),1)",sql);
            c.Parameters.AddWithValue("@source",row.Source);c.Parameters.AddWithValue("@hidden",state.Hidden);c.Parameters.AddWithValue("@date",(object?)state.DeletedAt??DBNull.Value);c.Parameters.AddWithValue("@manual",state.Manual);c.Parameters.AddWithValue("@reason",state.Reason);await c.ExecuteNonQueryAsync();
        }
    }
    static async Task<object> ApplyVisibility(MySqlConnection sql,Dictionary<string,JsonObject> records) {
        // The importer and this phase use the same mutex. No withdrawals happen after a failed import.
        await using(var c=new MySqlCommand("SELECT GET_LOCK('alda_munreco_import',0)",sql))if(Convert.ToInt32(await c.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Otra importacion esta en marcha; visibilidad pendiente");
        try {
            var rows=await ReadRows(sql);await EnsureStates(sql,rows);rows=await ReadRows(sql);
            int hidden=0,shown=0,manual=0,own=0;var items=new List<object>();
            await using var tx=await sql.BeginTransactionAsync();
            foreach(var row in rows) {
                bool currentHidden;DateTime? currentDate;
                await using(var read=new MySqlCommand("SELECT eliminado,fecha_eliminado FROM producto WHERE id=@id FOR UPDATE",sql,tx)) {
                    read.Parameters.AddWithValue("@id",row.Product);await using var r=await read.ExecuteReaderAsync();
                    if(!await r.ReadAsync())throw new InvalidOperationException("Un producto ha cambiado durante la sincronizacion");
                    currentHidden=r.GetBoolean(0);currentDate=r.IsDBNull(1)?null:r.GetDateTime(1);
                }
                var present=records.TryGetValue(row.Source,out var record);
                var quantity=present?int.Parse(record!["supplierQuantity"]!.ToString()):0;
                var review=present && record!["requiresReview"]?.GetValue<bool>()==true;
                var decision=SyncPolicy.Decide(row.Created,currentHidden,currentDate,row.Managed!,present,quantity,review);
                if(!row.Created)own++;else if(decision.Manual)manual++;
                if(decision.Hidden!=currentHidden) {
                    currentDate=decision.Hidden?new DateTime(DateTime.UtcNow.Ticks/TimeSpan.TicksPerSecond*TimeSpan.TicksPerSecond,DateTimeKind.Unspecified):null;
                    await using var change=new MySqlCommand("UPDATE producto SET eliminado=@hidden,fecha_eliminado=@date WHERE id=@id",sql,tx);
                    change.Parameters.AddWithValue("@hidden",decision.Hidden);change.Parameters.AddWithValue("@date",(object?)currentDate??DBNull.Value);change.Parameters.AddWithValue("@id",row.Product);await change.ExecuteNonQueryAsync();
                    if(decision.Hidden)hidden++;else shown++;
                    items.Add(new{reference=row.Reference,productId=row.Product,hidden=decision.Hidden,reason=decision.Reason});
                }
                await using var update=new MySqlCommand("UPDATE producto_proveedor_sync SET oculto=@hidden,fecha_oculto=@date,manual=@manual,motivo=@reason,presente_proveedor=@present,fecha_comprobacion=UTC_TIMESTAMP(6) WHERE proveedor='Munreco' AND referencia_externa=@source",sql,tx);
                update.Parameters.AddWithValue("@present",present);
                update.Parameters.AddWithValue("@source",row.Source);update.Parameters.AddWithValue("@hidden",decision.Hidden);update.Parameters.AddWithValue("@date",(object?)currentDate??DBNull.Value);update.Parameters.AddWithValue("@manual",decision.Manual);update.Parameters.AddWithValue("@reason",decision.Reason);await update.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();return new{hidden,shown,manualPreserved=manual,originalCatalogPreserved=own,items};
        } finally {await using var c=new MySqlCommand("SELECT RELEASE_LOCK('alda_munreco_import')",sql);await c.ExecuteScalarAsync();}
    }
}
