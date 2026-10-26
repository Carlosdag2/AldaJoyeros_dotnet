using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services;

// Guest carts contain only IDs and quantities. Prices and availability always come from the catalog.
public sealed class GuestCartService(ICarritoService accounts, IProductoService products, ICartCatalog catalog,
    IHttpContextAccessor accessor, ILogger<GuestCartService> logger)
{
    private HttpContext Context => accessor.HttpContext ?? throw new InvalidOperationException("No hay sesión de carrito.");
    public List<string> Notices { get; } = new();
    public bool PendingMerge => TempCarritoHelper.HasItems(Context);

    public async Task<IReadOnlyList<CarritoItemDto>> GetAsync(long? userId)
    {
        if (userId.HasValue) {
            await MergeAsync(userId.Value);
            return (await accounts.GetByUsuarioIdAsync(userId.Value)).ToArray();
        }
        var result = new List<CarritoItemDto>();
        foreach (var item in TempCarritoHelper.GetItems(Context)) {
            var product = await products.GetByIdAsync(item.ProductoId);
            if (product == null || product.Eliminado || !await catalog.IsPurchasableAsync(item.ProductoId)) {
                TempCarritoHelper.RemoveItem(Context, item.ProductoId);
                Notices.Add("Se ha retirado un artículo que ya no está disponible. Revisa tu carrito.");
                continue;
            }
            result.Add(new CarritoItemDto { Id = item.ProductoId, ProductoId = item.ProductoId,
                ProductoNombre = product.Nombre, Producto = product, Precio = product.Precio,
                Cantidad = item.Cantidad, Subtotal = product.Precio * item.Cantidad });
        }
        return result;
    }

    public async Task<string> AddAsync(long? userId, long productId, int quantity)
    {
        if (quantity is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
        var product = await products.GetByIdAsync(productId);
        if (product == null || product.Eliminado) throw new InvalidOperationException("El producto ya no está disponible.");
        if (!await catalog.IsPurchasableAsync(productId)) throw new InvalidOperationException("Selecciona una talla disponible en la ficha del producto.");
        if (userId.HasValue) await accounts.AddItemAsync(userId.Value, new() { ProductoId = productId, Cantidad = quantity });
        else TempCarritoHelper.AddItem(Context, productId, quantity);
        return product.Nombre;
    }
    public async Task UpdateAsync(long? userId, long itemId, int quantity) {
        if (quantity is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
        if (userId.HasValue) await accounts.UpdateItemAsync(userId.Value, itemId, new() { Cantidad = quantity });
        else TempCarritoHelper.UpdateItem(Context, itemId, quantity);
    }
    public async Task RemoveAsync(long? userId, long itemId) {
        if (userId.HasValue) await accounts.DeleteItemAsync(userId.Value, itemId);
        else TempCarritoHelper.RemoveItem(Context, itemId);
    }
    public async Task ClearAsync(long? userId) {
        if (userId.HasValue) await accounts.ClearCarritoAsync(userId.Value);
        else TempCarritoHelper.Clear(Context);
    }

    // Remove only confirmed transfers: retries never add the already transferred lines again.
    public async Task<bool> MergeAsync(long userId)
    {
        if (!PendingMerge || Context.Items[StoreModeStore.ContextKey] is not true) return true;
        foreach (var item in TempCarritoHelper.GetItems(Context)) {
            try {
                var product = await products.GetByIdAsync(item.ProductoId);
                if (product == null || product.Eliminado || !await catalog.IsPurchasableAsync(item.ProductoId)) {
                    Notices.Add("Un artículo ya no está disponible y no se ha incorporado al carrito.");
                } else {
                    var existing = (await accounts.GetByUsuarioIdAsync(userId)).FirstOrDefault(i => i.ProductoId == item.ProductoId);
                    var quantity = Math.Min(item.Cantidad, Math.Max(0, 99 - (existing?.Cantidad ?? 0)));
                    if (quantity > 0) await accounts.AddItemAsync(userId, new() { ProductoId = item.ProductoId, Cantidad = quantity });
                    if (quantity < item.Cantidad) Notices.Add("Al combinar tus carritos se ha aplicado el límite de 99 unidades por artículo.");
                }
                TempCarritoHelper.RemoveItem(Context, item.ProductoId);
                await Context.Session.CommitAsync();
            } catch (Exception ex) {
                logger.LogError(ex, "No se pudo transferir el carrito temporal al usuario {UserId}", userId);
                Notices.Add("No se ha podido guardar todo el carrito. Tus artículos pendientes se conservan; vuelve a intentarlo antes de comprar.");
                return false;
            }
        }
        return true;
    }
}
