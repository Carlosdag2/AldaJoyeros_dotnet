using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AldaJoyeros.Services.Munreco;

public sealed class MunrecoWorker(MunrecoStore store,MunrecoSession session,MunrecoPaths paths,IOptions<MunrecoOptions> options,IConfiguration config,ILogger<MunrecoWorker> logger):BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(!options.Value.WorkerEnabled)return;
        while(!stoppingToken.IsCancellationRequested) {
            try {
                await store.Ensure(stoppingToken);
                await using var leader=store.Connection(false);await leader.OpenAsync(stoppingToken);
                await using(var c=new MySqlCommand("SELECT GET_LOCK('alda_munreco_web_worker',0)",leader)) {
                    if(Convert.ToInt32(await c.ExecuteScalarAsync(stoppingToken))!=1){await Task.Delay(TimeSpan.FromSeconds(15),stoppingToken);continue;}
                }
                await store.RecoverInterrupted(stoppingToken);
                while(!stoppingToken.IsCancellationRequested) {
                    await using(var ping=new MySqlCommand("SELECT 1",leader))await ping.ExecuteScalarAsync(stoppingToken);
                    await store.ScheduleDue(stoppingToken);
                    var job=(await store.Jobs(stoppingToken)).FirstOrDefault(x=>x.State=="queued");
                    if(job!=null)await Run(job,stoppingToken);
                    else await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken);
                }
            } catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception e) {logger.LogWarning("La sincronización Munreco no está disponible ({ErrorType}). Se reintentará la conexión.",e.GetType().Name);await Task.Delay(TimeSpan.FromSeconds(30),stoppingToken);}
        }
    }
    async Task Run(MunrecoJob job,CancellationToken ct) {
        await store.Update(job.Id,"running","Conectando con el catálogo de Munreco",ct:ct);
        var privateRun=Path.Combine(paths.Private,job.Id);Directory.CreateDirectory(privateRun);
        var auth=Path.Combine(privateRun,"input-auth.json");
        try {
            if(!session.Connected){await store.Update(job.Id,"needs_login","Conecta tu cuenta B2B desde el panel antes de actualizar.");return;}
            await File.WriteAllTextAsync(auth,await session.Read(ct),ct);
            var start=session.StartInfo();
            foreach(var arg in new[]{job.Trigger=="preview"?"--plan":"--apply","--run-id",job.Id,"--auth",auth,"--image-store",paths.Images,"--history",paths.History,"--private-history",paths.Private})start.ArgumentList.Add(arg);
            // Pass only database settings to the child, without writing credentials to disk or command arguments.
            start.Environment["ALDA_MUNRECO_CONNECTIONS"]=JsonSerializer.Serialize(new{ConnectionStrings=new{DefaultConnection=config.GetConnectionString("DefaultConnection")},MongoDbSettings=new{ConnectionString=config["MongoDbSettings:ConnectionString"],DatabaseName=config["MongoDbSettings:DatabaseName"]}});
            using var process=new Process{StartInfo=start};process.Start();process.StandardInput.Close();
            using var stop=ct.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
            var stderr=process.StandardError.ReadToEndAsync();bool needsLogin=false;
            while(await process.StandardOutput.ReadLineAsync(ct) is { } line) {
                if(line.StartsWith("SECTION "))await store.Update(job.Id,"running","Leyendo las secciones del catálogo…",ct:ct);
                else if(line.StartsWith("ALL_CATALOGS_COMPLETE"))await store.Update(job.Id,"running","Comparando productos y preparando las imágenes…",ct:ct);
                else if(line.StartsWith("IMPORTED "))await store.Update(job.Id,"running","Guardando productos e imágenes: "+line.Split(' ')[1],ct:ct);
                else if(line.StartsWith("IMPORT_FINISHED"))await store.Update(job.Id,"running","Comprobando el resultado en las bases de datos…",ct:ct);
            }
            await process.WaitForExitAsync(ct);var error=await stderr;
            needsLogin=error.Contains("authorization",StringComparison.OrdinalIgnoreCase)||error.Contains("Session expired",StringComparison.OrdinalIgnoreCase)||error.Contains("Section selection failed",StringComparison.OrdinalIgnoreCase);
            var result=Path.Combine(paths.Run(job.Id),job.Trigger=="preview"?"comparacion.json":"resultado.json");
            if(process.ExitCode==0&&File.Exists(result)) {
                var json=await File.ReadAllTextAsync(result,ct);using var report=JsonDocument.Parse(json);
                if(job.Trigger!="preview"&&report.RootElement.GetProperty("status").GetString()!="completo")throw new InvalidOperationException("Incomplete result");
                var refreshed=Path.Combine(privateRun,"auth.json");if(File.Exists(refreshed))await session.Save(await File.ReadAllTextAsync(refreshed,ct),ct);
                await store.Update(job.Id,job.Trigger=="preview"?"previewed":"completed",job.Trigger=="preview"?"Comparación completada. No se han cambiado productos de la tienda.":"Actualización terminada y verificada",json,ct);
            } else await store.Update(job.Id,needsLogin?"needs_login":"failed",needsLogin?"La sesión B2B ha caducado. Renueva el acceso y vuelve a actualizar.":"La actualización se ha detenido. Revisa el informe antes de volver a intentarlo; no se han aplicado retiradas a partir de una lectura incompleta.");
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) {
            await store.Update(job.Id,"interrupted","La aplicación se detuvo durante la actualización. Puedes volver a ejecutarla.");throw;
        } catch(Exception e) {
            logger.LogWarning("Actualización Munreco {JobId} detenida ({ErrorType}).",job.Id,e.GetType().Name);
            await store.Update(job.Id,"failed","No se ha podido completar la actualización. Comprueba la conexión y la configuración del servidor.");
        } finally {
            foreach(var file in new[]{auth,Path.Combine(privateRun,"auth.json")})if(File.Exists(file))File.Delete(file);
        }
    }
}
