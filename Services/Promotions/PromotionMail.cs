using System.Net;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace AldaJoyeros.Services.Promotions;
public sealed class PromotionMail(IDataProtectionProvider provider)
{
    readonly IDataProtector protector = provider.CreateProtector("AldaJoyeros.Promotions.Unsubscribe.v1");
    public string Token(long id, string version) => protector.Protect(id + "|" + version);
    public (long Id, string Version)? Parse(string? token)
    {
        try
        {
            var values = protector.Unprotect(token ?? "").Split('|');
            return values.Length == 2 && long.TryParse(values[0], out var id) && id > 0 ? (id, values[1]) : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public static string Render(string subject, string origin, IEnumerable<PromotionEmailProduct> products, string token)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        static string M(decimal n) => H(n.ToString("C", CultureInfo.GetCultureInfo("es-ES")));
        var rows = string.Join("", products.Select(p => $"<tr><td style='padding:24px;border-bottom:1px solid #e7e5e4'>{(p.Image == null ? "" : $"<a href='{H(p.Url)}'><img src='{H(p.Image)}' alt='{H(p.Name)}' width='220' style='display:block;width:100%;max-width:220px;height:auto;margin:0 auto 20px;background:#fff'></a>")}<h2 style='font-size:19px'>{H(p.Name)}</h2><p><span style='color:#737373;font-size:12px'>PVP sin descuento: </span><s style='color:#737373'>{M(p.BasePrice??p.Previous)}</s> <strong style='color:#065f46;font-size:23px'>{M(p.Price)}</strong></p><p style='color:#065f46'>−{H(p.Percent.ToString("0.##", CultureInfo.GetCultureInfo("es-ES")))} % sobre PVP</p>{(p.BasePrice.HasValue&&p.Previous!=p.BasePrice.Value?$"<p style='font-size:12px;color:#626d66'>Menor precio previo registrado (30 días): {M(p.Previous)}</p>":"")}<a href='{H(p.Url)}' style='display:inline-block;background:#065f46;color:white;padding:14px 22px;border-radius:8px;text-decoration:none'>Ver producto</a></td></tr>"));
        var unsubscribe = origin.TrimEnd('/') + "/Promociones/Baja?token=" + Uri.EscapeDataString(token);
        return $"<!doctype html><html lang='es'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head><body style='margin:0;background:#f5f5f4;color:#292524;font-family:Arial,sans-serif'><table role='presentation' width='100%'><tr><td align='center' style='padding:24px 12px'><table role='presentation' width='600' style='width:100%;max-width:600px;background:white;border-radius:16px'><tr><td style='padding:32px 24px;background:#064e3b;color:white'><p style='font-size:14px;letter-spacing:2px'>ALDA JOYEROS 1962 · OFERTAS</p><h1 style='font-size:28px'>{H(subject)}</h1><p>Una selección especial, ahora con descuento.</p></td></tr>{rows}<tr><td style='padding:24px'><a href='{H(origin.TrimEnd('/') + "/catalogo?descuentos=si")}'>Ver todos los productos con descuento</a><p style='font-size:12px;line-height:20px;color:#57534e'>Precios vigentes al preparar este correo. Consulta en la ficha el precio y disponibilidad actuales. En artículos con tallas se indica el precio desde. Precios de contado, salvo error tipográfico.</p><p style='font-size:12px'>Recibes este correo porque te has suscrito a las ofertas de Alda Joyeros 1962. <a href='{H(unsubscribe)}'>Dejar de recibir ofertas</a>, sin iniciar sesión.</p></td></tr></table></td></tr></table></body></html>";
    }
}
