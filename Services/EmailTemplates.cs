using System.Globalization;
using System.Net;
using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services;

// Tablas y estilos en línea para que el contenido también funcione sin el CSS del <head>.
internal static class EmailTemplates
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Money(double value) => H(value.ToString("C2", Spanish));
    private static string OrderUrl(string baseUrl, long id) => $"{baseUrl.TrimEnd('/')}/Pedidos/Detalle/{id}";

    public static string Order(PedidoDto pedido, string invoice, string baseUrl)
    {
        var rows = string.Join("", pedido.LineasPedido.Select(line => $"""
            <tr>
              <td valign="top" style="padding:18px 12px 18px 0;border-bottom:1px solid #e7e9e4;word-break:break-word;">
                <p style="margin:0 0 6px;font-size:15px;line-height:22px;font-weight:bold;color:#192e27;">{H(string.IsNullOrWhiteSpace(line.ProductoNombre) ? "Artículo" : line.ProductoNombre)}</p>
                <p style="margin:0;font-size:13px;line-height:20px;color:#626d66;">Cantidad: {line.Cantidad} &nbsp;·&nbsp; {Money(line.Precio)} / ud.</p>
              </td>
              <td width="100" align="right" valign="top" style="padding:18px 0;border-bottom:1px solid #e7e9e4;font-size:15px;line-height:22px;font-weight:bold;color:#192e27;">{Money(line.Subtotal)}</td>
            </tr>
            """));
        var delivery = pedido.FechaEntregaEstimada.HasValue
            ? Notice("Entrega estimada", H(pedido.FechaEntregaEstimada.Value.ToString("d 'de' MMMM 'de' yyyy", Spanish)))
            : "";
        var address = pedido.Direccion is { } d
            ? SectionTitle("Dirección de envío") + $"""
                <p style="margin:0 0 24px;font-size:14px;line-height:24px;color:#626d66;">
                  {H(d.Calle)}, {H(d.Numero)}{(string.IsNullOrWhiteSpace(d.Piso) ? "" : ", " + H(d.Piso))}<br>
                  {H(d.CodigoPostal)} {H(d.Ciudad)}<br>{H(d.Provincia)}
                </p>
                """
            : "";
        var content = Hero("TU PEDIDO", "Una elección especial.", "Gracias por confiar en AldaJoyeros. Hemos recibido tu pedido; puedes consultar su estado y todos sus detalles desde tu cuenta.")
            + Summary(pedido, invoice)
            + SectionTitle("Tu selección")
            + $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="table-layout:fixed;">{rows}
                  <tr><td style="padding:20px 0 28px;font-size:15px;color:#192e27;">Total del pedido</td>
                  <td align="right" style="padding:20px 0 28px;font-size:20px;font-weight:bold;color:#192e27;">{Money(pedido.Total)}</td></tr>
                </table>
                """
            + address + delivery
            + Notice("Tu factura, adjunta", "Encontrarás la factura en PDF adjunta a este correo. Puedes guardarla o descargarla de nuevo desde el detalle del pedido.")
            + Button("Consultar mi pedido", OrderUrl(baseUrl, pedido.Id))
            + LinkFallback(OrderUrl(baseUrl, pedido.Id));
        return Layout("Confirmación de pedido", $"Pedido #{pedido.Id} recibido. Tu factura está adjunta en PDF.", content);
    }

    public static string NewOrder(PedidoDto pedido, string customerEmail, string paymentMethod, string baseUrl)
    {
        var url = $"{baseUrl.TrimEnd('/')}/AdminPedidos/Detalle/{pedido.Id}";
        var rows = string.Join("", pedido.LineasPedido.Select(line => $"""
            <tr>
              <td style="padding:14px 12px 14px 0;border-bottom:1px solid #e7e9e4;word-break:break-word;font-size:14px;line-height:22px;color:#192e27;">
                <strong>{H(string.IsNullOrWhiteSpace(line.ProductoNombre) ? "Artículo" : line.ProductoNombre)}</strong><br>
                <span style="color:#626d66;">Cantidad: {line.Cantidad} · {Money(line.Precio)} / ud.</span>
              </td>
              <td width="100" align="right" valign="top" style="padding:14px 0;border-bottom:1px solid #e7e9e4;font-size:14px;line-height:22px;color:#192e27;">{Money(line.Subtotal)}</td>
            </tr>
            """));
        var payment = paymentMethod == "Contra Reembolso"
            ? "Contra reembolso: pendiente de cobro al entregar."
            : paymentMethod == "Tarjeta" ? "Tarjeta: pago confirmado." : paymentMethod;
        var content = Hero("NUEVO PEDIDO", $"Has recibido el pedido #{pedido.Id}.", "Un cliente ha realizado un pedido en AldaJoyeros. Consulta los detalles y gestiona su preparación desde el panel de administración.")
            + Notice("Datos del pedido", $"Fecha: {H(pedido.Fecha?.ToString("d MMMM yyyy · HH:mm", Spanish) ?? "No disponible")}<br>Cliente: {H(customerEmail)}<br>Método de pago: {H(payment)}")
            + SectionTitle("Artículos del pedido")
            + $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="table-layout:fixed;">{rows}
                  <tr><td style="padding:20px 0 28px;font-size:15px;color:#192e27;">Total del pedido</td>
                  <td align="right" style="padding:20px 0 28px;font-size:20px;font-weight:bold;color:#192e27;">{Money(pedido.Total)}</td></tr>
                </table>
                """
            + Button("Gestionar pedido", url) + LinkFallback(url);
        return Layout("Nuevo pedido en tu tienda", $"Nuevo pedido #{pedido.Id}. Consulta los artículos y el importe en el panel.", content);
    }

    public static string Invoice(PedidoDto pedido, string invoice, string baseUrl)
    {
        var url = OrderUrl(baseUrl, pedido.Id);
        return Layout("Tu factura", $"La factura {invoice} de tu pedido #{pedido.Id}, adjunta en PDF.",
            Hero("TU FACTURA", "Todo en su sitio.", "Aquí tienes una copia de la factura de tu compra en AldaJoyeros. Gracias por elegirnos.")
            + Summary(pedido, invoice)
            + Notice("Documento PDF adjunto", "Abre el archivo adjunto para consultar o guardar tu factura. Este correo es un reenvío del documento; no supone una nueva compra ni un nuevo cobro.")
            + $"<p style=\"margin:0 0 28px;font-size:15px;line-height:24px;color:#626d66;\">Total del pedido: <strong style=\"color:#192e27;\">{Money(pedido.Total)}</strong></p>"
            + Button("Ver mi pedido", url) + LinkFallback(url));
    }

    public static string Password(string verifyLink, string code)
    {
        return Layout("Recuperar contraseña", "Tu código para recuperar el acceso a AldaJoyeros. Válido durante una hora.",
            Hero("TU CUENTA", "Vuelve a entrar.", "Hemos recibido una solicitud para restablecer tu contraseña. Utiliza este código en la página de verificación para recuperar el acceso a tu cuenta.")
            + $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 28px;background-color:#f3f6f2;border:1px solid #dce5dc;">
                  <tr><td align="center" style="padding:24px 12px;">
                    <p style="margin:0 0 12px;font-size:11px;letter-spacing:2px;color:#626d66;">CÓDIGO DE VERIFICACIÓN</p>
                    <p class="verification-code" style="margin:0;font-family:'Courier New',monospace;font-size:34px;font-weight:bold;letter-spacing:6px;line-height:46px;color:#14532d;">{H(code)}</p>
                    <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#626d66;">Caduca en 1 hora · Un solo uso</p>
                  </td></tr>
                </table>
                """
            + Button("Restablecer mi contraseña", verifyLink)
            + LinkFallback(verifyLink)
            + Notice("¿No has solicitado este cambio?", "Puedes ignorar este correo: tu contraseña seguirá siendo la misma. No compartas este código con nadie."));
    }

    private static string Hero(string label, string title, string text) => $"""
        <p style="margin:0 0 14px;font-size:11px;font-weight:bold;letter-spacing:2px;line-height:18px;color:#15803d;">{H(label)}</p>
        <h1 style="margin:0 0 18px;font-family:Georgia,'Times New Roman',serif;font-size:34px;line-height:42px;font-weight:normal;color:#192e27;">{H(title)}</h1>
        <p style="margin:0 0 30px;font-size:15px;line-height:26px;color:#626d66;">{H(text)}</p>
        """;

    private static string Summary(PedidoDto pedido, string invoice) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 28px;background-color:#f7f7f2;border:1px solid #e7e9e4;table-layout:fixed;">
          <tr>
            <td class="summary-cell" width="50%" valign="top" style="padding:20px;word-break:break-word;">
              <p style="margin:0 0 7px;font-size:11px;letter-spacing:1px;color:#626d66;">PEDIDO</p>
              <p style="margin:0 0 7px;font-size:18px;font-weight:bold;color:#192e27;">#{pedido.Id}</p>
              <p style="margin:0;font-size:13px;line-height:20px;color:#626d66;">{H(pedido.Fecha?.ToString("d MMMM yyyy", Spanish) ?? "Fecha no disponible")}</p>
            </td>
            <td class="summary-cell" width="50%" valign="top" style="padding:20px;word-break:break-word;">
              <p style="margin:0 0 7px;font-size:11px;letter-spacing:1px;color:#626d66;">FACTURA</p>
              <p style="margin:0 0 7px;font-size:15px;font-weight:bold;line-height:22px;color:#192e27;">{H(invoice)}</p>
              <p style="margin:0;font-size:13px;line-height:20px;color:#626d66;">PDF adjunto a este correo</p>
            </td>
          </tr>
        </table>
        """;

    private static string SectionTitle(string title) => $"<h2 style=\"margin:0 0 6px;font-size:16px;line-height:24px;color:#192e27;\">{H(title)}</h2>";

    private static string Notice(string title, string text) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 28px;background-color:#f7f7f2;">
          <tr><td style="padding:18px 20px;border-left:3px solid #b69b65;">
            <p style="margin:0 0 6px;font-size:14px;font-weight:bold;line-height:22px;color:#192e27;">{H(title)}</p>
            <p style="margin:0;font-size:13px;line-height:22px;color:#626d66;">{text}</p>
          </td></tr>
        </table>
        """;

    private static string Button(string label, string url) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 18px;">
          <tr><td align="center" bgcolor="#14532d" style="background-color:#14532d;border-radius:6px;mso-padding-alt:16px 24px;">
            <a href="{H(url)}" style="display:block;padding:16px 24px;color:#ffffff;font-family:Arial,sans-serif;font-size:14px;font-weight:bold;line-height:22px;text-decoration:none;text-align:center;border-radius:6px;">{H(label)}</a>
          </td></tr>
        </table>
        """;

    private static string LinkFallback(string url) => $"""
        <p style="margin:0 0 28px;font-size:12px;line-height:20px;color:#626d66;word-break:break-all;overflow-wrap:anywhere;">
          Si el botón no funciona, copia este enlace en tu navegador:<br>
          <a href="{H(url)}" style="color:#14532d;text-decoration:underline;">{H(url)}</a>
        </p>
        """;

    private static string Layout(string title, string preheader, string content) => $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="light">
          <title>{{H(title)}} · AldaJoyeros</title>
          <style>
            body,table,td,a { -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; }
            table,td { mso-table-lspace:0pt; mso-table-rspace:0pt; }
            @media only screen and (max-width:600px) {
              .outer-padding { padding:16px 8px !important; }
              .content-padding { padding:30px 22px 8px !important; }
              .brand-padding { padding:30px 20px !important; }
              .summary-cell { display:block !important; width:auto !important; padding:16px 20px !important; }
              .verification-code { font-size:30px !important; letter-spacing:4px !important; }
            }
          </style>
        </head>
        <body style="margin:0;padding:0;width:100%;background-color:#f1f2ed;font-family:Arial,Helvetica,sans-serif;">
          <div style="display:none;font-size:1px;line-height:1px;color:#f1f2ed;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;">{{H(preheader)}}</div>
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#f1f2ed">
            <tr><td class="outer-padding" align="center" style="padding:40px 16px;">
              <!--[if mso]><table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0"><tr><td><![endif]-->
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;background-color:#ffffff;border:1px solid #e1e5dd;">
                <tr><td class="brand-padding" align="center" bgcolor="#102d23" style="padding:36px 24px;background-color:#102d23;border-bottom:3px solid #b69b65;">
                  <p style="margin:0 0 8px;font-family:Georgia,'Times New Roman',serif;font-size:34px;letter-spacing:7px;line-height:40px;color:#ffffff;">ALDA</p>
                  <p style="margin:0 0 12px;font-size:11px;letter-spacing:5px;line-height:18px;color:#d9c69f;">JOYEROS</p>
                  <p style="margin:0;font-size:10px;letter-spacing:2px;line-height:16px;color:#c2cfc5;">DESDE 1962</p>
                </td></tr>
                <tr><td class="content-padding" style="padding:38px 40px 10px;">{{content}}</td></tr>
                <tr><td align="center" style="padding:24px 22px;background-color:#f7f7f2;border-top:1px solid #e7e9e4;">
                  <p style="margin:0 0 8px;font-family:Georgia,'Times New Roman',serif;font-size:18px;line-height:26px;color:#192e27;">Estamos para ayudarte.</p>
                  <p style="margin:0 0 6px;font-size:13px;line-height:22px;"><a href="mailto:aldajoyeros1962@gmail.com" style="color:#14532d;text-decoration:underline;">aldajoyeros1962@gmail.com</a></p>
                  <p style="margin:0;font-size:13px;line-height:22px;"><a href="tel:+34925541227" style="color:#14532d;text-decoration:none;">925 54 12 27</a></p>
                </td></tr>
              </table>
              <!--[if mso]></td></tr></table><![endif]-->
              <p style="margin:22px 0 6px;font-size:11px;line-height:19px;color:#626d66;text-align:center;">C/ Sor Livia Alcorta, 24 · 45200 Illescas, Toledo</p>
              <p style="margin:0;font-size:11px;line-height:19px;color:#626d66;text-align:center;">&copy; {{DateTime.Now.Year}} Alda Joyeros 1962</p>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
