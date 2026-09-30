using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;

static partial class MonthlySync {
    // One-time recovery for the first enrolment, using the verified initial-import evidence.
    public static async Task RepairInitialEnrollment(string config,string output) {
        var verifiedAt=InitialVerificationTime("outputs/munreco")??throw new InvalidOperationException("Falta la verificacion inicial");
        var coverage=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"cobertura.json")))!;
        if(DateTime.UtcNow-DateTime.Parse(coverage["completedAt"]!.ToString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal)>TimeSpan.FromHours(24))throw new InvalidOperationException("La lectura ha caducado");
        var result=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"resultado.json")))!.AsObject();
        if(result["status"]?.ToString()!="completo")throw new InvalidOperationException("Primero debe terminar la importacion");
        var before=JsonSerializer.Deserialize<List<SupplierRow>>(await File.ReadAllTextAsync(Path.Combine(output,"estado-antes.json")),Json)!;
        var records=Directory.GetFiles(Path.Combine(output,"productos"),"*.json").Select(f=>JsonNode.Parse(File.ReadAllText(f))!.AsObject()).ToDictionary(x=>x["sourceId"]!.ToString());
        using var settings=JsonDocument.Parse(await File.ReadAllTextAsync(config),new(){CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
        await using var sql=new MySqlConnection(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());await sql.OpenAsync();
        await using(var c=new MySqlCommand("SELECT GET_LOCK('alda_munreco_monthly',0)",sql))if(Convert.ToInt32(await c.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Sincronizacion en marcha");
        var repaired=new List<string>();
        try {
            await using(var c=new MySqlCommand("SELECT GET_LOCK('alda_munreco_import',0)",sql))if(Convert.ToInt32(await c.ExecuteScalarAsync())!=1)throw new InvalidOperationException("Importacion en marcha");
            try {
                await using var tx=await sql.BeginTransactionAsync();
                foreach(var row in before.Where(x=>x.Managed==null && x.Created && Initial(x).Manual && !Initial(x,verifiedAt).Manual)) {
                    await using var c=new MySqlCommand("UPDATE producto_proveedor_sync y JOIN producto_proveedor s ON s.proveedor=y.proveedor AND s.referencia_externa=y.referencia_externa JOIN producto p ON p.id=s.producto_id SET y.manual=0,y.motivo=@reason WHERE y.proveedor='Munreco' AND y.referencia_externa=@source AND y.manual=1 AND y.motivo='decision_manual' AND p.eliminado=@hidden AND p.fecha_eliminado <=> @date AND y.oculto=@hidden AND y.fecha_oculto <=> @date",sql,tx);
                    c.Parameters.AddWithValue("@reason",Initial(row,verifiedAt).Reason);c.Parameters.AddWithValue("@source",row.Source);c.Parameters.AddWithValue("@hidden",row.Hidden);c.Parameters.AddWithValue("@date",(object?)row.DeletedAt??DBNull.Value);
                    if(await c.ExecuteNonQueryAsync()==1)repaired.Add(row.Reference);
                }
                await tx.CommitAsync();
            } finally {await using var c=new MySqlCommand("SELECT RELEASE_LOCK('alda_munreco_import')",sql);await c.ExecuteScalarAsync();}
            var visibility=JsonSerializer.SerializeToNode(await ApplyVisibility(sql,records),Json)!.AsObject();
            await SyncVerification.Run(config,output);
            await Save(Path.Combine(output,"correccion-inicial.json"),new{at=DateTime.UtcNow,repaired,visibility});
            var prior=result["visibility"]!.AsObject();
            visibility["hidden"]=visibility["hidden"]!.GetValue<int>()+prior["hidden"]!.GetValue<int>();
            visibility["shown"]=visibility["shown"]!.GetValue<int>()+prior["shown"]!.GetValue<int>();
            foreach(var item in prior["items"]!.AsArray())visibility["items"]!.AsArray().Add(item!.DeepClone());
            result["visibility"]=visibility;result["completedAt"]=DateTime.UtcNow;result["verified"]=true;
            await Save(Path.Combine(output,"resultado.json"),result);Console.WriteLine("INSCRIPCIONES_INICIALES_CORREGIDAS: "+repaired.Count);
        } finally {await using var c=new MySqlCommand("SELECT RELEASE_LOCK('alda_munreco_monthly')",sql);await c.ExecuteScalarAsync();}
    }
}
