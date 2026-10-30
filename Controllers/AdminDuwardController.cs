using System.Text.Json;
using AldaJoyeros.Attributes;
using AldaJoyeros.Services.Duward;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace AldaJoyeros.Controllers;
[AdminOnly,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AdminDuwardController(DuwardStore store,DuwardSource source,IOptions<DuwardOptions> options):BaseController {
 [HttpGet]public async Task<IActionResult> Index(CancellationToken ct)=>View(new DuwardDashboard(await store.Jobs(ct),options.Value.WorkerEnabled));
 [HttpGet]public async Task<IActionResult> Estado(CancellationToken ct)=>Json((await store.Jobs(ct)).Select(x=>new{x.Id,x.State,x.Label,x.Progress,x.Active}));
 [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(32*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=32*1024*1024)]
 public async Task<IActionResult> Subir(List<IFormFile>? archivos,bool aplicar,bool retirarAusentes,CancellationToken ct) {
  if(!options.Value.WorkerEnabled){TempData["Warning"]="La importación está desactivada en este servidor.";return RedirectToAction(nameof(Index));}
  if(archivos==null||archivos.Count is <1 or >10||archivos.Any(x=>x.Length is 0 or >10*1024*1024||!Path.GetExtension(x.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase))||archivos.Sum(x=>x.Length)>30*1024*1024) {
   TempData["Warning"]="Selecciona de 1 a 10 CSV de Duward, hasta 10 MB por archivo y 30 MB en total.";return RedirectToAction(nameof(Index));
  }
  var temporary=Path.Combine(source.Root,"upload-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
  try {
   var paths=new List<string>();for(int i=0;i<archivos.Count;i++){var p=Path.Combine(temporary,$"catalogo-{i+1}.csv");await using(var stream=System.IO.File.Create(p))await archivos[i].CopyToAsync(stream,ct);paths.Add(p);}
   await source.Analyze(paths.ToArray(),ct);
   await System.IO.File.WriteAllTextAsync(Path.Combine(temporary,"lote.json"),JsonSerializer.Serialize(new{files=paths.Select(Path.GetFileName).ToArray(),retireMissing=retirarAusentes}),ct);
   var id=await store.Enqueue(aplicar?"manual_csv":"preview_csv",CurrentUserId?.ToString()??"admin",temporary,source,ct);
   TempData[id==null?"Warning":"Success"]=id==null?"Ya hay una comprobación o importación en marcha. Espera a que termine.":aplicar?"CSV validados. La actualización se ejecutará en segundo plano; consulta aquí su resultado.":"CSV validados. Se comprobarán productos e imágenes sin cambiar el catálogo de la tienda.";
  }catch(InvalidDataException e){TempData["Error"]=e.Message;}
  catch(InvalidOperationException e){TempData["Error"]=e.Message;}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
  catch(Exception){TempData["Error"]="No se pudo preparar la importación. Comprueba los archivos y vuelve a intentarlo.";}
  finally{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
  return RedirectToAction(nameof(Index));
 }
 [HttpGet]public async Task<IActionResult> Informe(string id,string tipo="resultado",CancellationToken ct=default) {
  if(!Guid.TryParseExact(id,"N",out _)||tipo is not ("resultado" or "comparacion" or "revision" or "error"))return NotFound();
  var job=(await store.Jobs(ct)).FirstOrDefault(x=>x.Id==id);if(job==null)return NotFound();var p=Path.Combine(source.Run(id),tipo+".json");if(!System.IO.File.Exists(p))return NotFound();
  var pdf=await AldaJoyeros.Services.SupplierReportPdf.GenerateAsync("Duward",id,job.Trigger,job.State,job.CreatedUtc,job.EndedUtc,p,ct);return File(pdf,"application/pdf",$"duward-{id}-{tipo}.pdf");
 }
}
