using System.Reflection;
using System.Text.Json;
using AldaJoyeros.Controllers;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

// No accede a bases de datos, Stripe ni SMTP: comprueba el flujo con servicios simulados.
foreach (var scenario in new[] { "normal", "cash", "customer-fails", "pdf-fails", "owner-fails", "payment-fails", "create-fails", "unconfirmed", "repeat" })
{
    var created = 0; var ownerCalls = 0; var customerCalls = 0;
    object?[]? ownerArgs = null;
    var paymentMethod = scenario == "cash" ? "Contra Reembolso" : "Tarjeta";
    var order = new PedidoDto { Id = 42, UsuarioId = 7, Total = 50 };
    var pedidoService = Stub.Make<IPedidoService>((method, _) => method == "CreateFromCarritoAsync"
        ? scenario == "create-fails" ? Task.FromException<PedidoDto>(new Exception("Creation failed")) : Create()
        : throw new Exception("Unexpected order call"));
    Task<PedidoDto> Create() { created++; return Task.FromResult(order); }
    var cart = Stub.Make<ICarritoService>((_, _) => Task.FromResult<IEnumerable<CarritoItemDto>>(
        created == 0 ? new[] { new CarritoItemDto() } : Array.Empty<CarritoItemDto>()));
    var payment = Stub.Make<IPaymentService>((_, _) => Task.FromResult(new PaymentStatusResult
        { Status = scenario == "payment-fails" ? "requires_payment_method" : "succeeded" }));
    var invoice = Stub.Make<IFacturaService>((method, _) => method == "GenerarNumeroFactura" ? "AJ-42"
        : scenario == "pdf-fails" ? Task.FromException<byte[]>(new Exception("PDF failed")) : Task.FromResult(new byte[] { 1 }));
    var email = Stub.Make<IEmailService>((method, args) =>
    {
        if (method == "SendNewOrderNotificationAsync")
        {
            ownerCalls++;
            ownerArgs = args;
            return scenario == "owner-fails" ? Task.FromException(new Exception("SMTP failed")) : Task.CompletedTask;
        }
        if (method == "SendOrderConfirmationWithInvoiceAsync")
        {
            customerCalls++;
            return scenario == "customer-fails" ? Task.FromException(new Exception("SMTP failed")) : Task.CompletedTask;
        }
        throw new Exception("Unexpected email call");
    });
    var context = new DefaultHttpContext { Session = new MemorySession() };
    context.Items["CurrentUser"] = new UsuarioDto { Id = 7, Email = "cliente@example.invalid" };
    context.Session.SetString("CheckoutData", JsonSerializer.Serialize(new CheckoutSessionData
    {
        Direccion = new DireccionDto { Calle = "Prueba", Numero = "1", CodigoPostal = "28001", Ciudad = "Madrid", Provincia = "Madrid" },
        DireccionConfirmada = true, MetodoPago = paymentMethod, PagoConfirmado = scenario != "unconfirmed", PaymentIntentId = "pi_test"
    }));
    var controller = new PedidosController(pedidoService, cart, Stub.Make<IDireccionService>((_, _) => throw new Exception()),
        Stub.Make<IProductoImagenService>((_, _) => throw new Exception()), payment, email, invoice, NullLogger<PedidosController>.Instance)
    { ControllerContext = new ControllerContext { HttpContext = context }, TempData = new TempDataDictionary(context, new MemoryTempData()) };
    var result = (RedirectToActionResult)await controller.FinalizarPedido();
    var rejected = scenario is "payment-fails" or "create-fails" or "unconfirmed";
    if (ownerCalls != (rejected ? 0 : 1) || created != (rejected ? 0 : 1)) throw new Exception("Wrong notification count: " + scenario);
    if (!rejected && (ownerArgs == null || !ReferenceEquals(ownerArgs[0], order) || (string)ownerArgs[1]! != "cliente@example.invalid" || (string)ownerArgs[2]! != paymentMethod))
        throw new Exception("Wrong owner notification data: " + scenario);
    if (!rejected && (result.ActionName != "PedidoCompletado" || context.Session.GetString("CheckoutData") != null))
        throw new Exception("Email failure interrupted the checkout: " + scenario);
    if (scenario == "repeat") { await controller.FinalizarPedido(); if (ownerCalls != 1 || created != 1) throw new Exception("Repeated notification"); }
    Console.WriteLine($"Correcto: {scenario} (cliente={customerCalls}, propietario={ownerCalls})");
}
var templateType = typeof(EmailService).Assembly.GetType("AldaJoyeros.Services.EmailTemplates")!;
var sample = new PedidoDto { Id = 42, Total = 50, Fecha = new DateTime(2026, 10, 8), LineasPedido = new() { new() { ProductoNombre = "Anillo <oro> & plata", Cantidad = 2, Precio = 25, Subtotal = 50 } } };
string Render(string method) => (string)templateType.GetMethod("NewOrder")!.Invoke(null, new object[] { sample, "cliente@example.invalid", method, "https://tienda.example" })!;
var html = Render("Contra Reembolso");
if (!html.Contains("pendiente de cobro") || html.Contains("pago confirmado") || !html.Contains("/AdminPedidos/Detalle/42") || !html.Contains("Anillo &lt;oro&gt; &amp; plata") || !html.Contains("50,00"))
    throw new Exception("Invalid owner notification template");
if (!Render("Tarjeta").Contains("pago confirmado")) throw new Exception("Invalid card payment label");
Console.WriteLine("Correcto: plantilla, enlace de administración, importes y método de pago. Sin correos reales.");

public class Stub : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    public static T Make<T>(Func<string, object?[]?, object?> handler) where T : class
    { var proxy = Create<T, Stub>(); ((Stub)(object)proxy).Handler = handler; return proxy; }
}
public class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> values = new();
    public bool IsAvailable => true; public string Id => "test"; public IEnumerable<string> Keys => values.Keys;
    public void Clear() => values.Clear(); public void Remove(string key) => values.Remove(key);
    public void Set(string key, byte[] value) => values[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => values.TryGetValue(key, out value);
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
public class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
