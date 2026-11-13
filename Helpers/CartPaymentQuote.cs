using System.Globalization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using AldaJoyeros.DTOs;

namespace AldaJoyeros.Helpers;
// The server freezes the priced lines when creating the payment intent. A later discount
// change must not change an already initiated payment, and a changed cart cannot reuse it.
public sealed record CartPaymentQuote(long UserId, string IntentId, string Fingerprint)
{
    public const string Key = "AldaCartPaymentQuote";
    public static string Signature(IEnumerable<CarritoItemDto> rows) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", rows.OrderBy(r => r.Id).Select(r => r.Id + ":" + r.ProductoId + ":" + r.Cantidad + ":" + ((decimal)r.Precio).ToString("0.00", CultureInfo.InvariantCulture))))));
    public static CartPaymentQuote? Get(HttpContext? context)
    {
        var raw = context?.Session.GetString(Key);
        if (raw == null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<CartPaymentQuote>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(HttpContext context, long userId, string intent, IEnumerable<CarritoItemDto> rows) => context.Session.SetString(Key, JsonSerializer.Serialize(new CartPaymentQuote(userId, intent, Signature(rows))));
    public bool Matches(long userId, string? intent, IEnumerable<CarritoItemDto> rows) => UserId == userId && IntentId == intent && Fingerprint == Signature(rows);
}
