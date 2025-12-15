using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using AldaJoyeros.Configuration;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using Microsoft.Extensions.Options;

namespace AldaJoyeros.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            await SendEmailWithAttachmentAsync(to, subject, htmlBody, null, null);
        }

        public async Task SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, byte[]? attachment, string? attachmentName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(new MailboxAddress("", to));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };

                // Añadir adjunto si existe
                if (attachment != null && !string.IsNullOrEmpty(attachmentName))
                {
                    bodyBuilder.Attachments.Add(attachmentName, attachment, new ContentType("application", "pdf"));
                }

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                
                if (_emailSettings.SmtpPort == 465)
                {
                    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.SslOnConnect);
                }
                else
                {
                    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
                }
                
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email enviado exitosamente a {To}{Attachment}", 
                    to, 
                    attachment != null ? $" con adjunto {attachmentName}" : "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a {To}", to);
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(string to, string verifyLink, string code)
        {
            var subject = "Código de verificación - Alda Joyeros";
            var htmlBody = GetPasswordResetEmailTemplate(verifyLink, code);
            await SendEmailAsync(to, subject, htmlBody);
        }

        public async Task SendOrderConfirmationWithInvoiceAsync(string to, PedidoDto pedido, byte[] facturaPdf, string numeroFactura)
        {
            var subject = $"Confirmación de pedido #{pedido.Id} - Alda Joyeros";
            var htmlBody = GetOrderConfirmationWithInvoiceEmailTemplate(pedido, numeroFactura);
            var attachmentName = $"Factura_{numeroFactura.Replace("/", "-")}.pdf";
            
            await SendEmailWithAttachmentAsync(to, subject, htmlBody, facturaPdf, attachmentName);
        }

        public async Task SendInvoiceEmailAsync(string to, PedidoDto pedido, byte[] facturaPdf, string numeroFactura)
        {
            var subject = $"Factura {numeroFactura} - Alda Joyeros";
            var htmlBody = GetInvoiceEmailTemplate(pedido, numeroFactura);
            var attachmentName = $"Factura_{numeroFactura.Replace("/", "-")}.pdf";
            
            await SendEmailWithAttachmentAsync(to, subject, htmlBody, facturaPdf, attachmentName);
        }

        private string GetOrderConfirmationWithInvoiceEmailTemplate(PedidoDto pedido, string numeroFactura)
        {
            var baseUrl = _emailSettings.BaseUrl.TrimEnd('/');
            var pedidoUrl = $"{baseUrl}/Pedidos/Detalle/{pedido.Id}";
            
            var productosHtml = string.Join("", pedido.LineasPedido.Select((linea, index) => 
            {
                var inicial = !string.IsNullOrEmpty(linea.ProductoNombre) 
                    ? linea.ProductoNombre[0].ToString().ToUpper() 
                    : "A";
                    
                return $@"
                <tr>
                    <td style='padding: 28px 0; border-bottom: 1px solid #eaeaea;'>
                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                            <tr>
                                <td width='90' valign='top'>
                                    <div style='width: 80px; height: 80px; background: linear-gradient(145deg, #1a1a1a 0%, #2d2d2d 100%); text-align: center; line-height: 80px;'>
                                        <span style='font-size: 28px; color: #d4af37; font-family: ""Times New Roman"", Georgia, serif; font-weight: 300;'>{inicial}</span>
                                    </div>
                                </td>
                                <td style='padding-left: 24px; vertical-align: top;'>
                                    <p style='margin: 0 0 10px 0; font-size: 15px; font-weight: 400; color: #1a1a1a; letter-spacing: 0.3px; line-height: 1.4;'>{linea.ProductoNombre}</p>
                                    <p style='margin: 0 0 6px 0; font-size: 13px; color: #888888; letter-spacing: 0.5px;'>Cantidad: {linea.Cantidad}</p>
                                    <p style='margin: 0; font-size: 15px; color: #1a1a1a; font-weight: 500; letter-spacing: 0.5px;'>{linea.Subtotal:C}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>";
            }));

            var direccionLinea1 = pedido.Direccion != null
                ? $"{pedido.Direccion.Calle}, {pedido.Direccion.Numero}"
                : "";
            var direccionLinea2 = pedido.Direccion != null
                ? $"{(!string.IsNullOrEmpty(pedido.Direccion.Piso) ? $"{pedido.Direccion.Piso}, " : "")}{pedido.Direccion.CodigoPostal} {pedido.Direccion.Ciudad}"
                : "";
            var direccionLinea3 = pedido.Direccion?.Provincia ?? "";

            var cantidadTotal = pedido.LineasPedido.Sum(l => l.Cantidad);
            var fechaPedido = pedido.Fecha?.ToString("d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES")) ?? "";
            var horaPedido = pedido.Fecha?.ToString("HH:mm") ?? "";

            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Pedido #{pedido.Id} - Alda Joyeros 1962</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5; font-family: ""Helvetica Neue"", Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; -moz-osx-font-smoothing: grayscale;'>
    
    <div style='display: none; max-height: 0; overflow: hidden;'>
        Pedido #{pedido.Id} confirmado · Factura {numeroFactura} adjunta · Entrega en 3-5 días laborables
    </div>
    
    <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f5f5f5;'>
        <tr>
            <td align='center' style='padding: 40px 20px;'>
                
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 620px; background-color: #ffffff;'>
                    
                    <!-- Header Premium -->
                    <tr>
                        <td style='background: linear-gradient(180deg, #0a0a0a 0%, #1a1a1a 100%); padding: 50px 40px; text-align: center;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <!-- Línea decorativa superior -->
                                        <div style='width: 60px; height: 1px; background: linear-gradient(90deg, transparent, #d4af37, transparent); margin: 0 auto 24px auto;'></div>
                                        
                                        <p style='margin: 0; font-size: 32px; font-weight: 300; color: #ffffff; letter-spacing: 12px; font-family: ""Times New Roman"", Georgia, serif;'>ALDA</p>
                                        <p style='margin: 8px 0 0 0; font-size: 13px; color: #d4af37; letter-spacing: 6px; font-weight: 400;'>JOYEROS</p>
                                        <p style='margin: 12px 0 0 0; font-size: 10px; color: #666666; letter-spacing: 3px;'>DESDE 1962</p>
                                        
                                        <!-- Línea decorativa inferior -->
                                        <div style='width: 60px; height: 1px; background: linear-gradient(90deg, transparent, #d4af37, transparent); margin: 24px auto 0 auto;'></div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Confirmación Principal -->
                    <tr>
                        <td style='padding: 60px 48px 50px 48px; text-align: center; background-color: #ffffff;'>
                            <!-- Icono de confirmación -->
                            <div style='width: 70px; height: 70px; border: 2px solid #1a1a1a; border-radius: 50%; margin: 0 auto 28px auto; line-height: 70px;'>
                                <span style='font-size: 28px; color: #1a1a1a;'>✓</span>
                            </div>
                            
                            <p style='margin: 0 0 12px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 3px; font-weight: 500;'>Confirmación de pedido</p>
                            <p style='margin: 0 0 20px 0; font-size: 28px; font-weight: 300; color: #1a1a1a; letter-spacing: 1px; font-family: ""Times New Roman"", Georgia, serif;'>Gracias por confiar en nosotros</p>
                            <p style='margin: 0; font-size: 14px; color: #666666; line-height: 1.8; max-width: 400px; margin: 0 auto;'>
                                Hemos recibido tu pedido y nuestro equipo ya está preparándolo con el cuidado que merece.
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Línea separadora elegante -->
                    <tr>
                        <td style='padding: 0 48px;'>
                            <div style='height: 1px; background: linear-gradient(90deg, transparent 0%, #e0e0e0 20%, #e0e0e0 80%, transparent 100%);'></div>
                        </td>
                    </tr>
                    
                    <!-- Información del Pedido -->
                    <tr>
                        <td style='padding: 40px 48px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td width='50%' valign='top'>
                                        <p style='margin: 0 0 8px 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 2px; font-weight: 500;'>Número de pedido</p>
                                        <p style='margin: 0; font-size: 24px; color: #1a1a1a; font-weight: 300; letter-spacing: 1px;'>#{pedido.Id}</p>
                                    </td>
                                    <td width='50%' valign='top' align='right'>
                                        <p style='margin: 0 0 8px 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 2px; font-weight: 500;'>Fecha del pedido</p>
                                        <p style='margin: 0; font-size: 14px; color: #1a1a1a;'>{fechaPedido}</p>
                                        <p style='margin: 4px 0 0 0; font-size: 13px; color: #888888;'>{horaPedido} h</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Aviso Factura -->
                    <tr>
                        <td style='padding: 0 48px 40px 48px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #fafafa; border-left: 3px solid #d4af37;'>
                                <tr>
                                    <td style='padding: 20px 24px;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td valign='middle'>
                                                    <p style='margin: 0 0 4px 0; font-size: 13px; color: #1a1a1a; font-weight: 500;'>Factura adjunta a este correo</p>
                                                    <p style='margin: 0; font-size: 12px; color: #888888;'>Nº {numeroFactura} · Documento PDF</p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Título Artículos -->
                    <tr>
                        <td style='padding: 0 48px 20px 48px;'>
                            <p style='margin: 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 3px; font-weight: 500;'>Tu selección ({cantidadTotal} {(cantidadTotal == 1 ? "artículo" : "artículos")})</p>
                        </td>
                    </tr>
                    
                    <!-- Productos -->
                    <tr>
                        <td style='padding: 0 48px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                {productosHtml}
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Resumen de Precio -->
                    <tr>
                        <td style='padding: 36px 48px 0 48px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td style='padding: 14px 0; border-bottom: 1px solid #eaeaea;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td><p style='margin: 0; font-size: 14px; color: #666666;'>Subtotal</p></td>
                                                <td align='right'><p style='margin: 0; font-size: 14px; color: #1a1a1a;'>{pedido.Total:C}</p></td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 14px 0; border-bottom: 1px solid #eaeaea;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td><p style='margin: 0; font-size: 14px; color: #666666;'>Envío</p></td>
                                                <td align='right'><p style='margin: 0; font-size: 14px; color: #1a1a1a;'>Cortesía</p></td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 24px 0 40px 0;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td><p style='margin: 0; font-size: 12px; color: #1a1a1a; text-transform: uppercase; letter-spacing: 2px; font-weight: 500;'>Total</p></td>
                                                <td align='right'><p style='margin: 0; font-size: 22px; color: #1a1a1a; font-weight: 400; letter-spacing: 0.5px;'>{pedido.Total:C}</p></td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Separador -->
                    <tr>
                        <td style='padding: 0 48px;'>
                            <div style='height: 1px; background-color: #1a1a1a;'></div>
                        </td>
                    </tr>
                    
                    <!-- Información de Entrega -->
                    <tr>
                        <td style='padding: 48px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td width='48%' valign='top'>
                                        <p style='margin: 0 0 16px 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 3px; font-weight: 500;'>Dirección de entrega</p>
                                        <p style='margin: 0 0 4px 0; font-size: 14px; color: #1a1a1a; line-height: 1.6;'>{direccionLinea1}</p>
                                        <p style='margin: 0 0 4px 0; font-size: 14px; color: #1a1a1a; line-height: 1.6;'>{direccionLinea2}</p>
                                        <p style='margin: 0; font-size: 14px; color: #1a1a1a; line-height: 1.6;'>{direccionLinea3}</p>
                                    </td>
                                    <td width='4%'></td>
                                    <td width='48%' valign='top'>
                                        <p style='margin: 0 0 16px 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 3px; font-weight: 500;'>Tiempo de entrega</p>
                                        <p style='margin: 0; font-size: 20px; color: #1a1a1a; font-weight: 300;'>3-5 días</p>
                                        <p style='margin: 6px 0 0 0; font-size: 13px; color: #888888;'>laborables</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Botón CTA -->
                    <tr>
                        <td style='padding: 0 48px 56px 48px;' align='center'>
                            <table cellpadding='0' cellspacing='0' border='0'>
                                <tr>
                                    <td style='background-color: #1a1a1a;'>
                                        <a href='{pedidoUrl}' style='display: inline-block; padding: 18px 56px; font-size: 11px; font-weight: 500; color: #ffffff; text-decoration: none; text-transform: uppercase; letter-spacing: 3px;'>
                                            Ver mi pedido
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Separador fino -->
                    <tr>
                        <td style='padding: 0 48px;'>
                            <div style='height: 1px; background-color: #eaeaea;'></div>
                        </td>
                    </tr>
                    
                    <!-- Atención al Cliente -->
                    <tr>
                        <td style='padding: 48px; text-align: center;'>
                            <p style='margin: 0 0 20px 0; font-size: 10px; color: #999999; text-transform: uppercase; letter-spacing: 3px; font-weight: 500;'>Atención personalizada</p>
                            <p style='margin: 0 0 8px 0; font-size: 14px; color: #1a1a1a;'>
                                <a href='mailto:aldajoyeros1962@gmail.com' style='color: #1a1a1a; text-decoration: none; border-bottom: 1px solid #d4af37;'>aldajoyeros1962@gmail.com</a>
                            </p>
                            <p style='margin: 0; font-size: 14px; color: #1a1a1a;'>
                                <a href='tel:+34925541227' style='color: #1a1a1a; text-decoration: none;'>+34 925 54 12 27</a>
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Footer Premium -->
                    <tr>
                        <td style='background: linear-gradient(180deg, #0a0a0a 0%, #1a1a1a 100%); padding: 48px; text-align: center;'>
                            <!-- Línea decorativa -->
                            <div style='width: 40px; height: 1px; background-color: #d4af37; margin: 0 auto 24px auto;'></div>
                            
                            <p style='margin: 0 0 6px 0; font-size: 20px; font-weight: 300; color: #ffffff; letter-spacing: 8px; font-family: ""Times New Roman"", Georgia, serif;'>ALDA JOYEROS 1962</p>
                            <p style='margin: 0 0 28px 0; font-size: 10px; color: #d4af37; letter-spacing: 4px;'>TRADICIÓN Y ELEGANCIA</p>
                            
                            <p style='margin: 0 0 6px 0; font-size: 12px; color: #666666;'>C/ Sor Livia Alcorta, 24</p>
                            <p style='margin: 0 0 28px 0; font-size: 12px; color: #666666;'>45200 Illescas, Toledo · España</p>
                            
                            <!-- Línea decorativa -->
                            <div style='width: 40px; height: 1px; background-color: #333333; margin: 0 auto 20px auto;'></div>
                            
                            <p style='margin: 0; font-size: 10px; color: #555555; letter-spacing: 1px;'>
                                © {DateTime.Now.Year} Alda Joyeros 1962. Todos los derechos reservados.
                            </p>
                        </td>
                    </tr>
                    
                </table>
                
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GetInvoiceEmailTemplate(PedidoDto pedido, string numeroFactura)
        {
            var baseUrl = _emailSettings.BaseUrl.TrimEnd('/');
            var pedidoUrl = $"{baseUrl}/Pedidos/Detalle/{pedido.Id}";

            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Factura {numeroFactura} - Alda Joyeros</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Arial, sans-serif;'>
    
    <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f4f4f5;'>
        <tr>
            <td align='center' style='padding: 32px 16px;'>
                
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 560px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background-color: #0f172a; padding: 32px; text-align: center;'>
                            <p style='margin: 0 0 4px 0; font-size: 22px; font-weight: 700; color: #ffffff; letter-spacing: 2px; font-family: Georgia, serif;'>ALDA JOYEROS</p>
                            <p style='margin: 0; font-size: 11px; color: #64748b; letter-spacing: 3px; text-align: center;'>DESDE 1962</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 36px 32px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <!-- Icon -->
                                        <div style='width: 64px; height: 64px; background: linear-gradient(135deg, #10b981 0%, #059669 100%); border-radius: 50%; margin: 0 auto 20px; line-height: 64px; text-align: center;'>
                                            <span style='font-size: 28px; color: #ffffff;'>📄</span>
                                        </div>
                                        
                                        <p style='margin: 0 0 8px 0; font-size: 20px; font-weight: 700; color: #0f172a;'>Tu factura está lista</p>
                                        <p style='margin: 0 0 28px 0; font-size: 14px; color: #64748b;'>
                                            Adjuntamos la factura de tu compra
                                        </p>
                                        
                                        <!-- Invoice Info Box -->
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f8fafc; border-radius: 12px; border: 1px solid #e2e8f0;'>
                                            <tr>
                                                <td style='padding: 24px;'>
                                                    <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                                        <tr>
                                                            <td width='50%'>
                                                                <p style='margin: 0 0 4px 0; font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px;'>Nº Factura</p>
                                                                <p style='margin: 0; font-size: 18px; font-weight: 700, color: #059669;'>{numeroFactura}</p>
                                                            </td>
                                                            <td width='50%' align='right'>
                                                                <p style='margin: 0 0 4px 0; font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px;'>Total</p>
                                                                <p style='margin: 0; font-size: 18px; font-weight: 700, color: #0f172a;'>{pedido.Total:c}</p>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                        </table>
                                        
                                        <p style='margin: 24px 0; font-size: 13px; color: #64748b;'>
                                            La factura está adjunta a este correo en formato PDF.<br/>
                                            También puedes consultar los detalles de tu pedido en nuestra web.
                                        </p>
                                        
                                        <!-- CTA -->
                                        <table cellpadding='0' cellspacing='0' border='0'>
                                            <tr>
                                                <td style='background-color: #0f172a; border-radius: 8px;'>
                                                    <a href='{pedidoUrl}' style='display: inline-block; padding: 14px 36px; font-size: 14px; font-weight: 600; color: #ffffff; text-decoration: none;'>
                                                        Ver pedido
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8fafc; padding: 24px 32px; border-top: 1px solid #e5e7eb;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <p style='margin: 0 0 8px 0; font-size: 13px; color: #64748b;'>
                                            ¿Tienes alguna pregunta sobre tu factura?
                                        </p>
                                        <p style='margin: 0; font-size: 13px;'>
                                            <a href='mailto:aldajoyeros1962@gmail.com' style='color: #059669; text-decoration: none; font-weight: 600;'>aldajoyeros1962@gmail.com</a>
                                            &nbsp;·&nbsp;
                                            <a href='tel:+34925541227' style='color: #059669; text-decoration: none; font-weight: 600;'>925 54 12 27</a>
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                </table>
                
                <!-- Copyright -->
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 560px;'>
                    <tr>
                        <td align='center' style='padding: 24px 16px;'>
                            <p style='margin: 0; font-size: 11px; color: #94a3b8;'>
                                &copy; {DateTime.Now.Year} Alda Joyeros 1962. Todos los derechos reservados.
                            </p>
                        </td>
                    </tr>
                </table>
                
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GetPasswordResetEmailTemplate(string verifyLink, string code)
        {
            var codeDigits = code.ToCharArray();
            
            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Código de Verificación - Alda Joyeros</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; -webkit-font-smoothing: antialiased;'>
    
    <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f4f4f5;'>
        <tr>
            <td align='center' style='padding: 32px 16px;'>
                
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 480px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background-color: #0f172a; padding: 28px 32px; text-align: center;'>
                            <p style='margin: 0 0 4px 0; font-size: 20px; font-weight: 700; color: #ffffff; letter-spacing: 2px; font-family: Georgia, serif;'>ALDA JOYEROS</p>
                            <p style='margin: 0; font-size: 10px; color: #64748b; letter-spacing: 3px; text-align: center;'>DESDE 1962</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 36px 32px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <p style='margin: 0 0 8px 0; font-size: 18px; font-weight: 700; color: #0f172a;'>Código de verificación</p>
                                        <p style='margin: 0 0 28px 0; font-size: 14px; color: #64748b;'>
                                            Introduce este código para restablecer tu contraseña
                                        </p>
                                    
                                        <!-- Code -->
                                        <table cellpadding='0' cellspacing='0' border='0' style='background-color: #f0fdf4; border-radius: 12px; border: 2px solid #10b981;'>
                                            <tr>
                                                <td style='padding: 20px 24px;'>
                                                    <table cellpadding='0' cellspacing='6' border='0'>
                                                        <tr>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[0]}</td>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[1]}</td>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[2]}</td>
                                                            <td style='width: 16px; text-align: center; color: #94a3b8; font-size: 20px;'>-</td>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[3]}</td>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[4]}</td>
                                                            <td style='width: 40px; height: 48px; background-color: #ffffff; border: 2px solid #bbf7d0; border-radius: 8px; text-align: center; font-size: 24px; font-weight: 700; color: #059669; font-family: monospace;'>{codeDigits[5]}</td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                        </table>
                                        
                                        <p style='margin: 24px 0; font-size: 13px; color: #64748b;'>
                                            Haz clic en el botón y luego introduce el código
                                        </p>
                                        
                                        <!-- CTA -->
                                        <table cellpadding='0' cellspacing='0' border='0'>
                                            <tr>
                                                <td style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); border-radius: 8px;'>
                                                    <a href='{verifyLink}' style='display: inline-block; padding: 14px 36px; font-size: 14px; font-weight: 600; color: #ffffff; text-decoration: none;'>
                                                        Verificar identidad
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                        
                                        <!-- Warning -->
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%' style='margin-top: 28px;'>
                                            <tr>
                                                <td style='background-color: #fef3c7; border-radius: 8px; padding: 14px 16px;'>
                                                    <p style='margin: 0; font-size: 12px; color: #92400e; text-align: center;'>
                                                        <strong>Expira en 1 hora</strong> · Solo puede usarse una vez
                                                    </p>
                                                </td>
                                            </tr>
                                        </table>
                                        
                                        <p style='margin: 20px 0 0 0; font-size: 12px; color: #94a3b8;'>
                                            Si no solicitaste este cambio, ignora este correo.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e5e7eb; text-align: center;'>
                            <p style='margin: 0; font-size: 11px; color: #94a3b8;'>
                                &copy; {DateTime.Now.Year} Alda Joyeros 1962
                            </p>
                        </td>
                    </tr>
                    
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}
