using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AldaJoyeros.Services.PerezMora;

public sealed class PerezMoraWorker(PerezMoraStore store,PerezMoraSource source,IOptions<PerezMoraOptions> options,IConfiguration config,ILogger<PerezMoraWorker> logger):BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct) {
        if(!options.Value.WorkerEnabled)return;
        while(!ct.IsCancellationRequested) {
            try {
                await store.Ensure(ct);await using var leader=store.Connection(false);await leader.OpenAsync(ct);
                var key="alda_perez_mora_worker_"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(leader.Database)))[..16];
                await using(var mutex=new MySqlCommand("SELECT GET_LOCK(@key,0)",leader)) {
                    mutex.Parameters.AddWithValue("@key",key);
                    if(Convert.ToInt32(await mutex.ExecuteScalarAsync(ct))!=1){await Task.Delay(TimeSpan.FromSeconds(15),ct);continue;}
                }
                await store.RecoverInterrupted(ct);
                while(!ct.IsCancellationRequested) {
                    await using(var ping=new MySqlCommand("SELECT 1",leader))await ping.ExecuteScalarAsync(ct);
                    await store.ScheduleDue(ct);
                    var job=(await store.Jobs(ct)).FirstOrDefault(x=>x.State=="queued");
                    if(job==null)await Task.Delay(TimeSpan.FromSeconds(10),ct);else await Run(job,ct);
                }
            }catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning("Pérez Mora no está disponible ({ErrorType}). Se reintentará.",e.GetType().Name);await Task.Delay(TimeSpan.FromSeconds(30),ct);}
        }
    }
    async Task Run(PerezMoraJob job,CancellationToken ct) {
        var run=source.Run(job.Id);Directory.CreateDirectory(run);var input=Path.Combine(run,"catalogo.xls");
        try {
            await store.Update(job.Id,"running","Preparando el catálogo de Pérez Mora",ct:ct);
            var upload=job.Trigger.EndsWith("_upload",StringComparison.Ordinal);
            if(!upload) {
                if(!source.Connected){await store.Update(job.Id,"needs_login","Guarda el enlace de descarga de tu cuenta Pérez Mora.");return;}
                await File.WriteAllBytesAsync(input,await source.Download(ct),ct);
            }
            if(!File.Exists(input))throw new InvalidDataException("Falta el Excel de esta tarea.");
            var start=source.StartInfo();var preview=job.Trigger.StartsWith("preview",StringComparison.Ordinal);
            foreach(var arg in new[]{preview?"--plan":"--apply","--input",input,"--root",run,"--images",source.Images})start.ArgumentList.Add(arg);
            start.Environment["ALDA_PEREZMORA_CONNECTIONS"]=JsonSerializer.Serialize(new{ConnectionStrings=new{DefaultConnection=config.GetConnectionString("DefaultConnection")},MongoDbSettings=new{ConnectionString=config["MongoDbSettings:ConnectionString"],DatabaseName=config["MongoDbSettings:DatabaseName"]}});
            using var process=new Process{StartInfo=start};process.Start();
            using var stop=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
            var errors=process.StandardError.ReadToEndAsync();
            while(await process.StandardOutput.ReadLineAsync(ct) is {} line) {
                if(line.StartsWith("CATALOG "))await store.Update(job.Id,"running","Catálogo validado. Comparando referencias…",ct:ct);
                else if(line.StartsWith("IMAGES "))await store.Update(job.Id,"running","Descargando y comprobando imágenes: "+line[7..],ct:ct);
                else if(line.StartsWith("IMPORTED "))await store.Update(job.Id,"running","Guardando productos e imágenes: "+line[9..],ct:ct);
                else if(line.StartsWith("VERIFIED "))await store.Update(job.Id,"running","Verificación terminada",ct:ct);
            }
            await process.WaitForExitAsync(ct);var error=await errors;
            var result=Path.Combine(run,preview?"comparacion.json":"resultado.json");
            if(process.ExitCode!=0||!File.Exists(result)) {
                await File.WriteAllTextAsync(Path.Combine(run,"error.json"),JsonSerializer.Serialize(new{message=error.Trim(),interruptedAt=DateTime.UtcNow}),ct);
                await store.Update(job.Id,"failed","La importación se ha detenido. Consulta el informe; puedes reintentar sin duplicar productos.");return;
            }
            var json=await File.ReadAllTextAsync(result,ct);using var parsed=JsonDocument.Parse(json);
            if(!preview&&parsed.RootElement.GetProperty("status").GetString()!="completo")throw new InvalidDataException("Resultado incompleto.");
            // Keep only the summary in the job queue; detailed references remain in the protected downloadable report.
            var summary=parsed.RootElement.EnumerateObject().Where(x=>x.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)).ToDictionary(x=>x.Name,x=>x.Value.Clone());
            await store.Update(job.Id,preview?"previewed":"completed",preview?"Comparación completada. Consulta las referencias afectadas.":"Actualización terminada y verificada",JsonSerializer.Serialize(summary),ct);
        }catch(OperationCanceledException) when(ct.IsCancellationRequested){await store.Update(job.Id,"interrupted","La aplicación se detuvo durante la importación. Puedes reintentar.");throw;}
        catch(Exception e){logger.LogWarning("Pérez Mora {JobId} detenido ({ErrorType}).",job.Id,e.GetType().Name);await store.Update(job.Id,"failed","No se ha podido descargar o procesar el catálogo. Revisa el enlace y las conexiones.");}
    }
}
