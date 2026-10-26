using AldaJoyeros.Services.Implementations;
using AldaJoyeros.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
var checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
var clock=new TestClock();var traffic=new TraficoService(clock);
var visitor=Guid.NewGuid().ToString("N");var first=Guid.NewGuid().ToString("N");var second=Guid.NewGuid().ToString("N");
Check(traffic.ObtenerSnapshot().VisitantesActivos==0,"Estado inicial sin visitantes ficticios");
traffic.RegistrarVisita(visitor,"/catalogo");
Check(traffic.ObtenerSnapshot().VistasUltimos30Minutos==1&&traffic.ObtenerSnapshot().VisitantesActivos==1,"La primera página confirma actividad incluso antes de ejecutar JavaScript");
traffic.RegistrarActividad(visitor,first,"/catalogo",1);
Check(traffic.ObtenerVisitantesActivos()==1&&traffic.ObtenerVisitantesPorPagina()["Catálogo"]==1,"Actividad visible correctamente agrupada");
clock.Advance(TimeSpan.FromSeconds(1));traffic.RegistrarActividad(visitor,second,"/producto/anillo-oro",1);
Check(traffic.ObtenerVisitantesActivos()==1&&traffic.ObtenerVisitantesPorPagina()["Ficha de producto"]==1,"Varias pestañas no duplican el visitante");
traffic.CerrarPestana(visitor,first,2);
Check(traffic.ObtenerVisitantesActivos()==1,"Cerrar una pestaña conserva otra visible");
traffic.CerrarPestana(visitor,second,2);
Check(traffic.ObtenerVisitantesActivos()==0,"Cerrar la última pestaña retira inmediatamente la presencia");
traffic.RegistrarActividad(visitor,second,"/catalogo",1);
Check(traffic.ObtenerVisitantesActivos()==0,"Una señal antigua retrasada no revive una pestaña cerrada");
traffic.RegistrarActividad(visitor,second,"/carrito",3);
Check(traffic.ObtenerVisitantesActivos()==1,"Volver a mostrar una pestaña restablece su presencia");
Check(traffic.ObtenerSnapshot().VistasUltimos30Minutos==1,"Los latidos no inflan las páginas abiertas");
clock.Advance(TimeSpan.FromSeconds(119));Check(traffic.ObtenerVisitantesActivos()==1,"Una señal reciente sigue activa");
clock.Advance(TimeSpan.FromSeconds(1));Check(traffic.ObtenerVisitantesActivos()==0,"Caducidad a los 120 segundos si se pierde el cierre");
traffic.RegistrarActividad(visitor,first,"/catalogo",4);traffic.RetirarConsentimiento(visitor);
Check(traffic.ObtenerVisitantesActivos()==0&&traffic.ObtenerSnapshot().VistasUltimos30Minutos==0,"Retirar consentimiento elimina presencia y visitas identificables");
traffic.RegistrarActividad(visitor,first,"/catalogo",5);traffic.RegistrarVisita(visitor,"/catalogo");
Check(traffic.ObtenerVisitantesActivos()==0&&traffic.ObtenerSnapshot().VistasUltimos30Minutos==0,"Las señales que ya estaban en vuelo no reactivan el seguimiento retirado");
var next=Guid.NewGuid().ToString("N");traffic.RegistrarVisita(next,"/privacidad");traffic.RegistrarActividad(next,first,"/privacidad",1);
Check(traffic.ObtenerVisitantesActivos()==1&&traffic.ObtenerSnapshot().VisitantesUltimos30Minutos==1,"Un consentimiento nuevo usa un identificador distinto");
clock.Advance(TimeSpan.FromMinutes(30));var snapshot=traffic.ObtenerSnapshot();
Check(snapshot.VistasUltimos30Minutos==0&&snapshot.VisitantesUltimos30Minutos==0,"Las ventanas de tráfico de 30 minutos expiran");
Check(snapshot.Historial.Count==15&&snapshot.Historial.All(h=>h.Visitantes==0),"Historial reciente acotado y sin datos antiguos");
var profiles=new TraficoService(clock);var incognito=Guid.NewGuid().ToString("N");
profiles.RegistrarActividad(visitor,first,"/catalogo",1);
profiles.RegistrarActividad(incognito,second,"/producto/anillo",1,visible:false);
Check(profiles.ObtenerVisitantesActivos()==2,"Incógnito cuenta como visitante distinto aunque quede en segundo plano");
profiles.RegistrarActividad(visitor,first,"/catalogo",2,visible:false);
Check(profiles.ObtenerVisitantesActivos()==2&&profiles.ObtenerSnapshot().VisitantesPorPagina.Values.Sum()==2,"Cambiar de ventana no elimina al visitante ni su sección");
profiles.CerrarPestana(incognito,second,2);
Check(profiles.ObtenerVisitantesActivos()==1,"Cerrar incógnito sí elimina su presencia");
foreach(var values in new[]{new[]{0d},new[]{1d,6d},new[]{0d,50.25d,3400.92d},new[]{0.01d}}){
 foreach(var integer in new[]{false,true}){var axis=MetricChartScale.Create(values,integer);Check(axis.Maximum>=values.Max()&&double.IsFinite(axis.Maximum)&&axis.Step>0&&(!integer||axis.Step==Math.Ceiling(axis.Step)),"Escala numérica cubre los valores y conserva enteros cuando procede");}
}
foreach(var path in new[]{"/Admin","/Admin/ObtenerTrafico","/Auth/ResetPassword","/Pedidos/Detalle/1","/Productos/Filtrar","/api/prueba","/CatalogoImagen/Foto/x","/catalogo?busqueda=privado","https://ajeno.example/catalogo","//ajeno.example/catalogo"})Check(TrafficPages.Resolve(path)==null,"Ruta privada, técnica o con información sensible excluida: "+path);
Check(TrafficPages.Resolve("/catalogo/anillos")=="Catálogo"&&TrafficPages.Resolve("/Pedidos/CheckoutPago")=="Proceso de compra","Rutas limpias y checkout agrupados correctamente");
var request=new DefaultHttpContext().Request;request.Headers.UserAgent="ExampleBot/1.0";Check(TrafficPages.IsAutomated(request),"Robot reconocible excluido");request.Headers.UserAgent="Mozilla/5.0 Chrome/130 Safari/537.36";Check(!TrafficPages.IsAutomated(request),"Navegador normal admitido");
foreach(var days in new[]{7,30,90}){var period=MetricsPeriod.Create(new DateTime(2026,10,26),days);Check((period.End-period.Start).TotalDays==days&&period.End.AddDays(-1)==new DateTime(2026,10,26),"El periodo de "+days+" días incluye hoy sin perder el último día");}
Check(MetricsPeriod.Create(new DateTime(2026,10,26),999).Days==30,"Periodo desconocido normalizado a 30 días");
Check(MetricsPeriod.MadridNow(clock).Kind==DateTimeKind.Unspecified,"Fechas de informe expresadas en la zona de Madrid");
// Verify actual HTTP response classification and cookie creation, without a database.
var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
var httpTraffic = new TraficoService(clock);
builder.Services.AddSingleton<AldaJoyeros.Services.Interfaces.ITraficoService>(httpTraffic);
await using (var app = builder.Build()) {
    app.UseMiddleware<AldaJoyeros.Middleware.TraficoMiddleware>();
    app.MapGet("/catalogo", () => Microsoft.AspNetCore.Http.Results.Content("<html>Catálogo</html>", "text/html"));
    app.MapGet("/producto/inexistente", () => Microsoft.AspNetCore.Http.Results.Content("No existe", "text/html", statusCode:404));
    app.MapGet("/catalogo/json", () => Microsoft.AspNetCore.Http.Results.Json(new { success=true }));
    app.MapGet("/privacidad", () => Microsoft.AspNetCore.Http.Results.Redirect("/cookies", permanent:true));
    app.MapGet("/Admin", () => Microsoft.AspNetCore.Http.Results.Content("Admin", "text/html"));
    await app.StartAsync();
    try {
        var url = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect=false });
        async Task<HttpResponseMessage> Get(string path, bool consent=true, string agent="Mozilla/5.0", bool ajax=false) {
            var message = new HttpRequestMessage(HttpMethod.Get,url+path);
            if(consent) message.Headers.Add("Cookie",VisitorConsent.CookieName+"=v1.analytics");
            message.Headers.Add("User-Agent",agent);
            if(ajax)message.Headers.Add("X-Requested-With","XMLHttpRequest");
            return await client.SendAsync(message);
        }
        foreach(var path in new[]{"/producto/inexistente","/catalogo/json","/privacidad","/Admin"}){
            var response=await Get(path);
            Check(!response.Headers.TryGetValues("Set-Cookie",out _)&&httpTraffic.ObtenerSnapshot().VistasUltimos30Minutos==0,"No registra error, JSON, redirección ni administrador: "+path);
        }
        await Get("/catalogo",false);await Get("/catalogo",agent:"ExampleBot/1.0");await Get("/catalogo",ajax:true);
        Check(httpTraffic.ObtenerSnapshot().VistasUltimos30Minutos==0,"No cuenta sin consentimiento, robots ni peticiones AJAX");
        var valid=await Get("/catalogo");
        Check(valid.Headers.TryGetValues("Set-Cookie",out var cookies)&&cookies.Any(c=>c.StartsWith(VisitorConsent.VisitorCookie+"=")),"Cookie de visita solo tras una página HTML correcta con consentimiento");
        Check(httpTraffic.ObtenerSnapshot().VistasUltimos30Minutos==1&&httpTraffic.ObtenerSnapshot().VisitantesActivos==1&&httpTraffic.ObtenerSnapshot().VisitantesPorPagina["Catálogo"]==1,"Página HTML registrada una vez con visitante activo y sección sin depender del latido");
    }finally{await app.StopAsync();}
}
Console.WriteLine($"{checks} comprobaciones superadas, sin bases de datos ni esperas reales.");
public sealed class TestClock:TimeProvider{private DateTimeOffset now=new(2026,10,26,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>now;public void Advance(TimeSpan elapsed)=>now+=elapsed;}
