using System.Text.Json;
using AldaJoyeros.Services;

var output=Path.GetFullPath(args.FirstOrDefault()??Path.Combine(Path.GetTempPath(),"alda-supplier-reports-"+Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(output);
var created=new DateTime(2026,10,15,10,0,0,DateTimeKind.Utc);
var id="0123456789abcdef0123456789abcdef";
async Task Pdf(string name,string type,object data,string provider="Pérez Mora",string state="completed",object? comparison=null)
{
    var dir=Path.Combine(output,name);Directory.CreateDirectory(dir);
    var file=Path.Combine(dir,type+".json");await File.WriteAllTextAsync(file,JsonSerializer.Serialize(data));
    if(comparison!=null)await File.WriteAllTextAsync(Path.Combine(dir,"comparacion.json"),JsonSerializer.Serialize(comparison));
    var bytes=await SupplierReportPdf.GenerateAsync(provider,id,"manual",state,created,created.AddMinutes(2),file);
    if(bytes.Length<1000||System.Text.Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")throw new Exception("PDF inválido: "+name);
    await File.WriteAllBytesAsync(Path.Combine(output,name+".pdf"),bytes);Console.WriteLine("PASS: "+name);
}
var changes=new {products=1200,added=2,restocked=1,soldOut=1,modified=1,removed=1,needsReview=1,collisions=1,
    pricingBands=new{A=2.5m,B=2m,C=1.8m},
    changes=new[]{new{reference="000012",action="nuevo",stock=5,review=false},new{reference="000013",action="nuevo",stock=0,review=true},new{reference="000014",action="reposicion",stock=12,review=false},new{reference="000015",action="modificado",stock=3,review=false},new{reference="000016",action="agotado",stock=0,review=false},new{reference="SIN-CAMBIOS",action="sin_cambios",stock=4,review=false}},
    retired=new[]{"000017"},conflictingReferences=new[]{"000018"},fileHash="DO_NOT_PRINT_TECHNICAL_HASH",password="DO_NOT_PRINT_SECRET"};
await Pdf("informe-ejemplo","comparacion",changes,state:"previewed");
await Pdf("resultado-perez-mora","resultado",new{products=1200,added=2,restocked=1,soldOut=1,modified=1,removed=1,needsReview=1,collisions=1,verifiedProducts=1199,linkedImages=2400,imageDownloadFailures=1,pricingBands=new{A=2.5m,B=2m,C=1.8m}},comparison:changes);
await Pdf("sin-cambios","comparacion",new{products=1,added=0,restocked=0,soldOut=0,modified=0,removed=0,changes=Array.Empty<object>()},state:"previewed");
await Pdf("revision","revision",new[]{new{reference="000013",reasons=new[]{"Sin existencias","Sin imágenes"},failedImages=new[]{"https://example.invalid/SECRET_DOWNLOAD_TOKEN"}}});
await Pdf("incidencia","error",new{errorType="TimeoutException",message="DO_NOT_PRINT_SECRET"},state:"failed");
await Pdf("incidencia-imagenes","error",new{errorType="MongoWriteException",message="DO_NOT_PRINT_SECRET"},state:"failed");
await Pdf("incidencia-historica-imagenes","error",new{message="Importación interrumpida (MongoWriteException). DO_NOT_PRINT_SECRET"},state:"failed");
await Pdf("historico-sin-detalle","resultado",new{products=12,added=1});
await Pdf("resultado-munreco","resultado",new{products=100,added=1,restocked=1,soldOut=1,removed=1,modified=1,existingPricesPreserved=true,
    visibility=new{hidden=1,shown=1,manualPreserved=2,originalCatalogPreserved=3,items=new[]{new{reference="REF-OCULTA",hidden=true,reason="sin_existencias"},new{reference="REF-ACTIVA",hidden=false,reason="disponible"}}}},provider:"Munreco",
    comparison:new{changes=new[]{new{reference="REF-PRECIO",type="datos_o_pvp_modificados",previousPvp="100.00",currentPriceText="120,00 €"}}});
await Pdf("verificacion","verificacion-mensual",new{totalProducts=27814,supplierProducts=2699,previousProductsPreserved=27814,linkedImages=7613,errors=Array.Empty<string>()},provider:"Munreco");
await Pdf("muchas-referencias","comparacion",new{products=180,modified=180,changes=Enumerable.Range(0,180).Select(i=>new{reference=$"REF-{i:D5}-VARIANTE-LARGA",type="datos_o_pvp_modificados",previousPvp="125.50",currentPriceText="139,00 €"})},provider:"Munreco",state:"previewed");
Console.WriteLine("PDFs de prueba: "+output);
