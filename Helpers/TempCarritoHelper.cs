using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AldaJoyeros.Helpers
{
    public static class TempCarritoHelper
    {
        private const string TempCarritoKey = "TempCarrito";

        public static void AddItem(HttpContext httpContext, long productoId, int cantidad = 1)
        {
            var items = GetItems(httpContext);
            
            var existingItem = items.FirstOrDefault(i => i.ProductoId == productoId);
            if (existingItem != null)
            {
                existingItem.Cantidad += cantidad;
            }
            else
            {
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
            var json = httpContext.Session.GetString(TempCarritoKey);
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
