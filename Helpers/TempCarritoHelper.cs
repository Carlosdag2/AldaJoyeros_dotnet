using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AldaJoyeros.Helpers
{
    public static class TempCarritoHelper
    {
        private const string TempCarritoKey = "TempCarrito";

        public static void AddItem(HttpContext httpContext, long productoId, int cantidad = 1)
        {
            if (productoId <= 0 || cantidad is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
            var items = GetItems(httpContext);
            
            var existingItem = items.FirstOrDefault(i => i.ProductoId == productoId);
            if (existingItem != null)
            {
                if (existingItem.Cantidad > 99 - cantidad) throw new InvalidOperationException("La cantidad máxima por producto es 99.");
                existingItem.Cantidad += cantidad;
            }
            else
            {
                if (items.Count >= 100) throw new InvalidOperationException("El carrito admite hasta 100 artículos diferentes.");
                items.Add(new TempCarritoItem 
                { 
                    ProductoId = productoId, 
                    Cantidad = cantidad 
                });
            }
            
            SaveItems(httpContext, items);
        }

        public static List<TempCarritoItem> GetItems(HttpContext httpContext)
        {
            var json = httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session?.GetString(TempCarritoKey);
            if (string.IsNullOrEmpty(json))
            {
                return new List<TempCarritoItem>();
            }
            
            try
            {
                return JsonSerializer.Deserialize<List<TempCarritoItem>>(json) ?? new List<TempCarritoItem>();
            }
            catch
            {
                return new List<TempCarritoItem>();
            }
        }

        public static void RemoveItem(HttpContext context, long productoId)
        {
            var items = GetItems(context);
            items.RemoveAll(i => i.ProductoId == productoId);
            SaveItems(context, items);
        }

        public static void UpdateItem(HttpContext context, long productoId, int cantidad)
        {
            if (cantidad is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
            var items = GetItems(context);
            var item = items.FirstOrDefault(i => i.ProductoId == productoId) ?? throw new KeyNotFoundException("Artículo no encontrado en tu carrito.");
            item.Cantidad = cantidad;
            SaveItems(context, items);
        }

        public static void Clear(HttpContext httpContext)
        {
            httpContext.Session.Remove(TempCarritoKey);
        }

        private static void SaveItems(HttpContext httpContext, List<TempCarritoItem> items)
        {
            var json = JsonSerializer.Serialize(items);
            httpContext.Session.SetString(TempCarritoKey, json);
        }

        public static bool HasItems(HttpContext httpContext)
        {
            return GetItems(httpContext).Any();
        }
    }

    public class TempCarritoItem
    {
        public long ProductoId { get; set; }
        public int Cantidad { get; set; }
    }
}
