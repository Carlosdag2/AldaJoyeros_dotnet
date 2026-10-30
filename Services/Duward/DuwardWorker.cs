using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AldaJoyeros.Services.Duward;

public sealed class DuwardWorker(DuwardStore store,DuwardSource source,IOptions<DuwardOptions> options,IConfiguration config,ILogger<DuwardWorker> logger):BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct) {
        if(!options.Value.WorkerEnabled)return;
        while(!ct.IsCancellationRequested) {
            try {
                await store.Ensure(ct);await using var leader=store.Connection(false);await leader.OpenAsync(ct);
                var key="alda_duward_worker_"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(leader.Database)))[..16];
                await using(var mutex=new MySqlCommand("SELECT GET_LOCK(@key,0)",leader)) {
                    mutex.Parameters.AddWithValue("@key",key);
                    if(Convert.ToInt32(await mutex.ExecuteScalarAsync(ct))!=1){await Task.Delay(TimeSpan.FromSeconds(15),ct);continue;}
                }
                foreach(var interrupted in (await store.Jobs(ct)).Where(x=>x.State=="running")) {
                    var folder=source.Run(interrupted.Id);Directory.CreateDirectory(folder);
                    await File.WriteAllTextAsync(Path.Combine(folder,"error.json"),JsonSerializer.Serialize(new{errorType="OperationCanceledException",message="La aplicación se reinició durante la importación.",interruptedAt=DateTime.UtcNow}),ct);
                }
                await store.Recover(ct);
                while(!ct.IsCancellationRequested) {
                    await using(var ping=new MySqlCommand("SELECT 1",leader))await ping.ExecuteScalarAsync(ct);

                    var job=(await store.Jobs(ct)).FirstOrDefault(x=>x.State=="queued");
                    if(job==null)await Task.Delay(TimeSpan.FromSeconds(10),ct);else await Run(job,ct);
                }
            }catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning("Duward no está disponible ({ErrorType}). Se reintentará.",e.GetType().Name);await Task.Delay(TimeSpan.FromSeconds(30),ct);}
        }
    }
    async Task Run(DuwardJob job,CancellationToken ct) {
        var run=source.Run(job.Id);Directory.CreateDirectory(run);var input=Path.Combine(run,"lote.json");
        try {
            await store.Update(job.Id,"running","Validando los CSV de Duward y buscando imágenes por referencia",ct:ct);
            if(!File.Exists(input))throw new InvalidDataException("Faltan los CSV de esta tarea.");
            var start=source.StartInfo();var preview=job.Trigger.StartsWith("preview",StringComparison.Ordinal);
            foreach(var arg in new[]{preview?"--plan":"--apply","--input",input,"--root",run,"--images",source.Images})start.ArgumentList.Add(arg);
            start.Environment["ALDA_DUWARD_CONNECTIONS"]=JsonSerializer.Serialize(new{ConnectionStrings=new{DefaultConnection=config.GetConnectionString("DefaultConnection")},MongoDbSettings=new{ConnectionString=config["MongoDbSettings:ConnectionString"],DatabaseName=config["MongoDbSettings:DatabaseName"]}});
            using var process=new Process{StartInfo=start};process.Start();
            using var stop=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
            var errors=process.StandardError.ReadToEndAsync();
            while(await process.StandardOutput.ReadLineAsync(ct) is {} line) {
                if(line.StartsWith("CATALOG "))await store.Update(job.Id,"running","Catálogo validado. Comparando referencias…",ct:ct);
                else if(line.StartsWith("ASSETS "))await store.Update(job.Id,"running","Verificando enlaces del CSV: "+line[7..],ct:ct);
                else if(line.StartsWith("IMAGES "))await store.Update(job.Id,"running","Comprobando referencias y recuperando imágenes: "+line[7..],ct:ct);
                else if(line.StartsWith("IMPORTED "))await store.Update(job.Id,"running","Guardando productos e imágenes: "+line[9..],ct:ct);
                else if(line.StartsWith("VERIFIED "))await store.Update(job.Id,"running","Verificación terminada",ct:ct);
            }
            await process.WaitForExitAsync(ct);var error=await errors;
            var result=Path.Combine(run,preview?"comparacion.json":"resultado.json");
            if(process.ExitCode!=0||!File.Exists(result)) {
                var errorType=System.Text.RegularExpressions.Regex.Match(error,@"\(([A-Za-z][A-Za-z0-9]*Exception)\)").Groups[1].Value;
                await File.WriteAllTextAsync(Path.Combine(run,"error.json"),JsonSerializer.Serialize(new{message=error.Trim(),errorType,validationReason=error.Contains("Retirada superior al 15%",StringComparison.Ordinal)?"retirement_limit":error.Contains("Ha aparecido una referencia coincidente",StringComparison.Ordinal)?"reference_changed":"",interruptedAt=DateTime.UtcNow}),ct);
                await store.Update(job.Id,"failed","La importación se ha detenido. Consulta el informe; puedes reintentar sin duplicar productos.");return;
            }
            var json=await File.ReadAllTextAsync(result,ct);using var parsed=JsonDocument.Parse(json);
            if(!preview&&parsed.RootElement.GetProperty("status").GetString()!="completo")throw new InvalidDataException("Resultado incompleto.");
            // Keep only the summary in the job queue; detailed references remain in the protected downloadable report.
            var summary=parsed.RootElement.EnumerateObject().Where(x=>x.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)).ToDictionary(x=>x.Name,x=>x.Value.Clone());
            await store.Update(job.Id,preview?"previewed":"completed",preview?"Comparación completada. Consulta las referencias afectadas.":"Actualización terminada y verificada",JsonSerializer.Serialize(summary),ct);
        }catch(OperationCanceledException) when(ct.IsCancellationRequested){await File.WriteAllTextAsync(Path.Combine(run,"error.json"),JsonSerializer.Serialize(new{errorType="OperationCanceledException",message="La aplicación se detuvo durante la importación.",interruptedAt=DateTime.UtcNow}));await store.Update(job.Id,"interrupted","La aplicación se detuvo durante la importación. Puedes reintentar.");throw;}
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(run,"error.json"),JsonSerializer.Serialize(new{errorType=e.GetType().Name,message="No se ha podido completar la importación.",interruptedAt=DateTime.UtcNow}));logger.LogWarning("Duward {JobId} detenido ({ErrorType}).",job.Id,e.GetType().Name);await store.Update(job.Id,"failed","No se ha podido procesar el lote. Revisa los CSV y las conexiones.");}
    }
}
