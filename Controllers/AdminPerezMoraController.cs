using AldaJoyeros.Attributes;
using AldaJoyeros.Services.PerezMora;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Controllers;

[AdminOnly]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AdminPerezMoraController(PerezMoraStore store,PerezMoraSource source,IOptions<PerezMoraOptions> options,IWebHostEnvironment env):BaseController {
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct)=>View(new PerezMoraDashboard(await store.Schedule(ct),await store.Jobs(ct),source.Connected,options.Value.WorkerEnabled,await store.Coefficient(ct)));
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Precios(string coeficiente,bool aplicar,CancellationToken ct) {
        if(!AldaJoyeros.Catalog.ProviderPricing.TryCoefficient(coeficiente,out var factor)){TempData["Warning"]="Introduce un coeficiente entre 0,01 y 100, con un máximo de cuatro decimales.";return RedirectToAction(nameof(Index));}
        try {
            var preview=await store.Prices(factor,aplicar,source.Root,ct);
            if(aplicar){TempData["Success"]=$"Coeficiente guardado. {preview.Updated} precios actualizados; {preview.Protected} precios manuales conservados.";return RedirectToAction(nameof(Index));}
            return View("Index",new PerezMoraDashboard(await store.Schedule(ct),await store.Jobs(ct),source.Connected,options.Value.WorkerEnabled,await store.Coefficient(ct),preview));
        }catch(InvalidOperationException e){TempData["Warning"]=e.Message;return RedirectToAction(nameof(Index));}
        catch(InvalidDataException e){TempData["Warning"]=e.Message;return RedirectToAction(nameof(Index));}
    }
    [HttpGet] public async Task<IActionResult> Estado(CancellationToken ct)=>Json((await store.Jobs(ct)).Select(x=>new{x.Id,x.State,x.Progress,x.Active}));
    [HttpPost,ValidateAntiForgeryToken] public Task<IActionResult> Comparar(CancellationToken ct)=>Enqueue("preview",ct);
    [HttpPost,ValidateAntiForgeryToken] public Task<IActionResult> Actualizar(CancellationToken ct)=>Enqueue("manual",ct);
    async Task<IActionResult> Enqueue(string trigger,CancellationToken ct) {
        if(!options.Value.WorkerEnabled||!source.Connected){TempData["Warning"]="Guarda el enlace del proveedor y habilita la actualización en el servidor.";return RedirectToAction(nameof(Index));}
        var id=await store.Enqueue(trigger,CurrentUserId?.ToString()??"admin",ct);
        TempData[id==null?"Warning":"Success"]=id==null?"Ya hay una tarea pendiente o en marcha.":"Tarea solicitada. Puedes cerrar el panel mientras se procesa.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Programar(PerezMoraSchedule schedule,CancellationToken ct) {
        if(!ModelState.IsValid){TempData["Warning"]="Elige un día de la semana y una hora válida.";return RedirectToAction(nameof(Index));}
        if(schedule.Enabled&&(!source.Connected||!options.Value.WorkerEnabled)){TempData["Warning"]="Guarda primero un enlace de descarga y habilita el proceso para programar actualizaciones.";return RedirectToAction(nameof(Index));}
        await store.SaveSchedule(schedule,ct);TempData["Success"]=schedule.Enabled?"Programación semanal guardada (hora de Madrid).":"Programación semanal desactivada.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(16384)] public async Task<IActionResult> Conectar(PerezMoraConnection connection,CancellationToken ct) {
        if(!env.IsDevelopment()&&!Request.IsHttps)return BadRequest("El enlace de cuenta requiere HTTPS.");
        if(!ModelState.IsValid||!PerezMoraSource.ValidUrl(connection.DownloadUrl)){TempData["Warning"]="Copia la dirección del botón de descarga del Excel en tu cuenta Pérez Mora.";return RedirectToAction(nameof(Index));}
        var temp=Path.Combine(source.Root,Guid.NewGuid().ToString("N")+".xls");
        try {
            await System.IO.File.WriteAllBytesAsync(temp,await source.Download(connection.DownloadUrl,ct),ct);await source.Analyze(temp,ct);await source.Save(connection.DownloadUrl,ct);
            TempData["Success"]="Descarga conectada y catálogo validado. Ya puedes comprobar cambios y programar actualizaciones.";
        }catch(InvalidDataException){TempData["Error"]="El enlace no ha descargado un catálogo válido. Revisa el botón de descarga de tu cuenta.";}
        catch(Exception){TempData["Error"]="No se pudo conectar con el proveedor. Vuelve a intentarlo.";}
        finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);connection.DownloadUrl="";ModelState.Clear();}
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(32*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=32*1024*1024)]
    public async Task<IActionResult> Subir(IFormFile? archivo,bool aplicar,CancellationToken ct) {
        if(!options.Value.WorkerEnabled){TempData["Warning"]="El proceso de actualización está desactivado en este servidor.";return RedirectToAction(nameof(Index));}
        if(archivo==null||archivo.Length is 0 or >30*1024*1024||!Path.GetExtension(archivo.FileName).Equals(".xls",StringComparison.OrdinalIgnoreCase)){TempData["Warning"]="Selecciona el Excel .xls original de Pérez Mora (máximo 30 MB).";return RedirectToAction(nameof(Index));}
        // Validate before enqueuing; never execute a job with a partial upload.
        var temp=Path.Combine(source.Root,Guid.NewGuid().ToString("N")+".xls");
        try {
            await using(var stream=System.IO.File.Create(temp))await archivo.CopyToAsync(stream,ct);
            var analysis=await source.Analyze(temp,ct);
            // The ready file is moved while holding a queue reservation so the worker cannot claim it prematurely.
            var id=await store.EnqueueUpload(aplicar?"manual_upload":"preview_upload",CurrentUserId?.ToString()??"admin",temp,source,ct);
            TempData[id==null?"Warning":"Success"]=id==null?"Ya hay una tarea pendiente o en marcha.":"Excel validado y tarea solicitada. Consulta el historial para ver el resultado.";
        }catch(InvalidDataException e){TempData["Error"]=e.Message;}
        catch(Exception){TempData["Error"]="No se pudo preparar el Excel. Comprueba el formato y vuelve a intentarlo.";}
        finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Informe(string id,string tipo="resultado",CancellationToken ct=default) {
        if(!Guid.TryParseExact(id,"N",out _)||tipo is not ("resultado" or "comparacion" or "revision" or "error")||!(await store.Jobs(ct)).Any(x=>x.Id==id))return NotFound();
        var file=Path.Combine(source.Run(id),tipo+".json");return System.IO.File.Exists(file)?PhysicalFile(file,"application/json",$"perez-mora-{id}-{tipo}.json"):NotFound();
    }
}
