using System.Reflection;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

var checks = 0;
void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
var context = new DefaultHttpContext();
context.Features.Set<ISessionFeature>(new TestSessionFeature { Session = new MemorySession() });
context.Items[StoreModeStore.ContextKey] = true;
var products = Products.Make();
var catalog = new Catalog();
var account = new AccountCart();
var cart = new GuestCartService(account, products, catalog, new HttpContextAccessor { HttpContext = context }, NullLogger<GuestCartService>.Instance);
await cart.AddAsync(null, 1, 2);
await cart.AddAsync(null, 1, 1);
var rows = await cart.GetAsync(null);
Check(rows.Count == 1 && rows[0].Cantidad == 3, "El invitado agrupa referencias y suma unidades");
Check(rows[0].Precio == 50 && rows[0].Subtotal == 150, "Precio y subtotal proceden del catálogo");
await cart.UpdateAsync(null, 1, 5);
Check((await cart.GetAsync(null))[0].Cantidad == 5, "El invitado puede cambiar cantidades");
foreach (var quantity in new[] { 0, -1, 100, int.MaxValue }) {
    try { await cart.AddAsync(null, 1, quantity); throw new Exception("Cantidad inválida aceptada"); }
    catch (InvalidOperationException) { Check(true, "Cantidad fuera de límites rechazada: " + quantity); }
}
try { await cart.AddAsync(null, 3, 1); throw new Exception("Padre sin talla aceptado"); }
catch (InvalidOperationException) { Check(true, "Exige elegir una talla disponible"); }
await cart.AddAsync(null, 2, 1);
account.Items.Add(new() { Id=10, ProductoId=1, Cantidad=2, Precio=50, Subtotal=100 });
Check(await cart.MergeAsync(7), "Transferencia a una cuenta existente completada");
Check(account.Items.Count == 2 && account.Items.Single(i=>i.ProductoId==1).Cantidad == 7, "Combina con el carrito guardado sin duplicar líneas");
Check(!TempCarritoHelper.HasItems(context), "Vacía el temporal tras confirmar los guardados");
await cart.MergeAsync(7);
Check(account.Items.Single(i=>i.ProductoId==1).Cantidad == 7, "Repetir la transferencia no duplica cantidades");
account.Items.Clear();
await cart.AddAsync(null, 1, 2); await cart.AddAsync(null, 2, 3); account.FailProduct=2;
Check(!await cart.MergeAsync(7), "Un fallo de guardado se comunica");
Check(TempCarritoHelper.GetItems(context).Single().ProductoId == 2, "Conserva el artículo fallido y retira solo lo guardado");
account.FailProduct=0;
Check(await cart.MergeAsync(7) && account.Items.Single(i=>i.ProductoId==1).Cantidad == 2 && account.Items.Single(i=>i.ProductoId==2).Cantidad == 3, "Reintento guarda pendientes sin repetir el primer artículo");
await cart.AddAsync(null, 1, 1);catalog.Unavailable.Add(1);
Check((await cart.GetAsync(null)).Count == 0 && cart.Notices.Count > 0, "Informa de artículos que dejan de estar disponibles");
catalog.Unavailable.Clear();
await cart.AddAsync(null, 2, 2);await cart.RemoveAsync(null, 2);
Check((await cart.GetAsync(null)).Count==0, "El invitado puede eliminar un artículo");
await cart.AddAsync(null, 1, 1);await cart.ClearAsync(null);
Check(!TempCarritoHelper.HasItems(context), "El invitado puede vaciar el carrito");
account.Items.Clear();account.Items.Add(new(){ Id=10,ProductoId=1,Cantidad=98,Precio=50 });await cart.AddAsync(null,1,5);
Check(await cart.MergeAsync(7) && account.Items.Single().Cantidad==99, "La combinación respeta el máximo de 99 unidades");
Console.WriteLine($"{checks} comprobaciones superadas; sin bases de datos, cuentas reales, correo ni pagos.");

public class MemorySession : ISession {
 private readonly Dictionary<string, byte[]> data = new();
 public string Id => "test"; public bool IsAvailable=>true;public IEnumerable<string> Keys=>data.Keys;
 public void Clear()=>data.Clear();public Task CommitAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
 public Task LoadAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
 public void Remove(string key)=>data.Remove(key);public void Set(string key,byte[] value)=>data[key]=value;
 public bool TryGetValue(string key,out byte[] value)=>data.TryGetValue(key,out value!);
}
public class Catalog : ICartCatalog {
 public HashSet<long> Unavailable {get;}=new();
 public Task<bool> IsPurchasableAsync(long id)=>Task.FromResult(id!=3&&!Unavailable.Contains(id));
}
public class Products : DispatchProxy {
 protected override object? Invoke(MethodInfo? method,object?[]? args) {
  if(method!.Name=="GetByIdAsync")return Task.FromResult<ProductoDto?>(new(){Id=(long)args![0]!,Nombre="Referencia",Precio=50});
  throw new Exception("Servicio inesperado: "+method.Name);
 }
 public static IProductoService Make()=>Create<IProductoService,Products>();
}
public class AccountCart : ICarritoService {
 public List<CarritoItemDto> Items{get;}=new();public long FailProduct;
 public Task<IEnumerable<CarritoItemDto>> GetByUsuarioIdAsync(long user)=>Task.FromResult<IEnumerable<CarritoItemDto>>(Items);
 public Task<CarritoItemDto> AddItemAsync(long user,CarritoItemCreateDto value){
  if(value.ProductoId==FailProduct)throw new InvalidOperationException("Fallo simulado");
  var item=Items.FirstOrDefault(x=>x.ProductoId==value.ProductoId);
  if(item==null){item=new(){Id=Items.Count+1,ProductoId=value.ProductoId,Precio=50};Items.Add(item);}
  item.Cantidad+=value.Cantidad;item.Subtotal=item.Cantidad*item.Precio;return Task.FromResult(item);
 }
 public Task<CarritoItemDto> UpdateItemAsync(long user,long id,CarritoItemUpdateDto value)=>throw new NotSupportedException();
 public Task DeleteItemAsync(long user,long id)=>throw new NotSupportedException();
 public Task ClearCarritoAsync(long user)=>throw new NotSupportedException();
 public Task<double> GetTotalAsync(long user)=>Task.FromResult(Items.Sum(x=>x.Subtotal));
 public Task<int> GetTotalItemsAsync(long user)=>Task.FromResult(Items.Sum(x=>x.Cantidad));
}

public sealed class TestSessionFeature : ISessionFeature { public ISession Session { get; set; } = new MemorySession(); }
