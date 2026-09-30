using AldaJoyeros.Attributes;
using AldaJoyeros.Services.Munreco;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Controllers;

[AdminOnly]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AdminMunrecoController(MunrecoStore store,MunrecoSession session,MunrecoPaths paths,IOptions<MunrecoOptions> options,IWebHostEnvironment environment):BaseController {
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)=>View(new MunrecoDashboard(await store.Schedule(ct),await store.Jobs(ct),session.Connected,options.Value.WorkerEnabled,session.UpdatedUtc));
    [HttpGet]
    public async Task<IActionResult> Estado(CancellationToken ct)=>Json((await store.Jobs(ct)).Select(x=>new{x.Id,x.State,x.Progress,x.Active}));
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(CancellationToken ct) {
        if(!options.Value.WorkerEnabled){TempData["Error"]="El proceso de actualización está desactivado en este servidor.";return RedirectToAction(nameof(Index));}
        if(!session.Connected){TempData["Warning"]="Conecta primero tu cuenta B2B de Munreco.";return RedirectToAction(nameof(Index));}
        var id=await store.Enqueue("manual",CurrentUserId?.ToString()??"admin",ct);
        TempData[id==null?"Warning":"Success"]=id==null?"Ya hay una actualización pendiente o en marcha.":"Actualización solicitada. Puedes cerrar esta página; el proceso seguirá en segundo plano.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Comparar(CancellationToken ct) {
        if(!session.Connected || !options.Value.WorkerEnabled){TempData["Warning"]="Conecta tu cuenta B2B y habilita el proceso de actualización antes de comparar.";return RedirectToAction(nameof(Index));}
        var id=await store.Enqueue("preview",CurrentUserId?.ToString()??"admin",ct);
        TempData[id==null?"Warning":"Success"]=id==null?"Ya hay una tarea pendiente o en marcha.":"Comparación solicitada. Consultará el proveedor sin cambiar los productos de tu tienda.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Programar(MunrecoSchedule schedule,CancellationToken ct) {
        if(!ModelState.IsValid){TempData["Warning"]="Elige un día entre 1 y 28 y una hora entre 0 y 23.";return RedirectToAction(nameof(Index));}
        await store.SaveSchedule(schedule,ct);TempData["Success"]=schedule.Enabled?"Actualización mensual guardada. Se utilizará la hora de Madrid.":"Actualización mensual desactivada.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(16384)]
    public async Task<IActionResult> Conectar(MunrecoLogin login,CancellationToken ct) {
        if(!environment.IsDevelopment()&&!Request.IsHttps)return BadRequest("El acceso B2B requiere HTTPS.");
        if(!ModelState.IsValid){TempData["Warning"]="Introduce el usuario y la contraseña B2B.";return RedirectToAction(nameof(Index));}
        try {await session.Connect(login,ct);TempData["Success"]="Acceso a Munreco conectado. Ya puedes actualizar el catálogo.";}
        catch(OperationCanceledException){TempData["Warning"]="Munreco no ha respondido a tiempo. Vuelve a intentarlo.";}
        catch(Exception){TempData["Error"]="No se ha podido validar el acceso B2B. Comprueba las credenciales y si el proveedor requiere una verificación adicional.";}
        finally {login.Password="";ModelState.Clear();}
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Informe(string id,string tipo="resultado",CancellationToken ct=default) {
        if(!Guid.TryParseExact(id,"N",out _) || tipo is not ("resultado" or "comparacion" or "verificacion-mensual" or "error"))return NotFound();
        if(!(await store.Jobs(ct)).Any(x=>x.Id==id))return NotFound();
        var path=Path.Combine(paths.Run(id),tipo+".json");if(!System.IO.File.Exists(path))return NotFound();
        return PhysicalFile(path,"application/json",$"munreco-{id}-{tipo}.json");
    }
}
