using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using AldaJoyeros.Configuration;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var settings = Options.Create(new EmailSettings { BaseUrl = "https://localhost:7155" });
var order = new PedidoDto { Id = 42, Total = 139 };
var cases = new (string Scheme, string Host, string Path, string Expected)[] {
    ("https", "tienda.red-privada.ts.net", "", "https://tienda.red-privada.ts.net"),
    ("https", "www.aldajoyeros.example", "", "https://www.aldajoyeros.example"),
    ("https", "catalogo.example:8443", "/joyeria", "https://catalogo.example:8443/joyeria"),
    ("http", "localhost:5038", "", "http://localhost:5038")
};
int checks = 0;
void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
string Render(EmailService service, string method, params object[] args) =>
    (string)typeof(EmailService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service,args)!;
void Verify(EmailService service, string origin)
{
    foreach (var (method, args, path) in new[] {
        ("GetOrderConfirmationWithInvoiceEmailTemplate", new object[] { order, "AJ-42" }, "/Pedidos/Detalle/42"),
        ("GetInvoiceEmailTemplate", new object[] { order, "AJ-42" }, "/Pedidos/Detalle/42"),
        ("GetNewOrderNotificationEmailTemplate", new object[] { order, "cliente@example.invalid", "Tarjeta" }, "/AdminPedidos/Detalle/42")
    }) {
        var html=WebUtility.HtmlDecode(Render(service,method,args));
        var urls=Regex.Matches(html,"href=\"([^\"]+)\"").Select(m=>m.Groups[1].Value).ToArray();
        Check(urls.Contains(origin+path),method+" apunta a "+origin+path);
        Check(!urls.Any(u=>u.StartsWith("http")&&!u.StartsWith(origin+"/",StringComparison.Ordinal)),"Sin enlaces a otro servidor en "+method);
    }
}
foreach (var test in cases)
{
    var context=new DefaultHttpContext();context.Request.Scheme=test.Scheme;context.Request.Host=new HostString(test.Host);context.Request.PathBase=test.Path;
    // No se confía en cabeceras sin procesar: debe usarse el origen que ha validado el servidor/proxy.
    context.Request.Headers["X-Forwarded-Host"]="dominio-ajeno.example";
    Verify(new EmailService(settings,NullLogger<EmailService>.Instance,new HttpContextAccessor{HttpContext=context}),test.Expected);
}
Verify(new EmailService(Options.Create(new EmailSettings{BaseUrl="https://tienda.example/joyeria/"}),NullLogger<EmailService>.Instance),"https://tienda.example/joyeria");
Console.WriteLine($"{checks} comprobaciones superadas. No se han enviado correos.");
