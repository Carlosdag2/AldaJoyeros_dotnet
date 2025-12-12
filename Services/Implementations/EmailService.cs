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

                _logger.LogInformation("Email enviado exitosamente a {To}", to);
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

        public async Task SendOrderConfirmationEmailAsync(string to, PedidoDto pedido)
        {
            var subject = $"Confirmación de pedido #{pedido.Id} - Alda Joyeros";
            var htmlBody = GetOrderConfirmationEmailTemplate(pedido);
            await SendEmailAsync(to, subject, htmlBody);
        }

        private string GetOrderConfirmationEmailTemplate(PedidoDto pedido)
        {
            var baseUrl = _emailSettings.BaseUrl.TrimEnd('/');
            var pedidoUrl = $"{baseUrl}/Pedidos/Detalle/{pedido.Id}";
            
            // Generar filas de productos con diseño mejorado sin dependencia de imágenes
            var productosHtml = string.Join("", pedido.LineasPedido.Select((linea, index) => 
            {
                var bgColor = index % 2 == 0 ? "#ffffff" : "#fafafa";
                
                // Obtener inicial del producto para el avatar
                var inicial = !string.IsNullOrEmpty(linea.ProductoNombre) 
                    ? linea.ProductoNombre.Substring(0, 1).ToUpper() 
                    : "P";
                
                // Colores para el avatar basados en el índice
                var avatarColors = new[] { 
                    ("#10b981", "#059669"), // Esmeralda
                    ("#3b82f6", "#2563eb"), // Azul
                    ("#8b5cf6", "#7c3aed"), // Violeta
                    ("#f59e0b", "#d97706"), // Ámbar
                    ("#ec4899", "#db2777")  // Rosa
                };
                var (colorFrom, colorTo) = avatarColors[index % avatarColors.Length];
                
                return $@"
                <tr style='background-color: {bgColor};'>
                    <td style='padding: 16px 24px; border-bottom: 1px solid #f0f0f0;'>
                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                            <tr>
                                <td width='56' valign='middle'>
                                    <div style='width: 48px; height: 48px; background: linear-gradient(135deg, {colorFrom} 0%, {colorTo} 100%); border-radius: 10px; text-align: center; line-height: 48px;'>
                                        <span style='font-size: 20px; font-weight: 700; color: #ffffff; font-family: Georgia, serif;'>{inicial}</span>
                                    </div>
                                </td>
                                <td style='padding-left: 14px; vertical-align: middle;'>
                                    <p style='margin: 0 0 4px 0; font-size: 15px; font-weight: 600; color: #1a1a1a; line-height: 1.3;'>{linea.ProductoNombre}</p>
                                    <p style='margin: 0; font-size: 13px; color: #666666;'>
                                        {linea.Cantidad} x {linea.Precio:C}
                                    </p>
                                </td>
                                <td width='90' align='right' valign='middle'>
                                    <p style='margin: 0; font-size: 16px; font-weight: 700; color: #059669;'>{linea.Subtotal:C}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>";
            }));

            var direccionHtml = pedido.Direccion != null
                ? $"{pedido.Direccion.Calle}, {pedido.Direccion.Numero}{(!string.IsNullOrEmpty(pedido.Direccion.Piso) ? $" - {pedido.Direccion.Piso}" : "")}<br>{pedido.Direccion.CodigoPostal} {pedido.Direccion.Ciudad}, {pedido.Direccion.Provincia}"
                : "No disponible";

            var cantidadTotal = pedido.LineasPedido.Sum(l => l.Cantidad);

            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Confirmación de Pedido #{pedido.Id} - Alda Joyeros</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; -webkit-font-smoothing: antialiased;'>
    
    <!-- Preheader -->
    <div style='display: none; max-height: 0; overflow: hidden; mso-hide: all;'>
        ¡Gracias por tu compra! Tu pedido #{pedido.Id} ha sido confirmado. Total: {pedido.Total:C}. Entrega en 3-5 días laborables.
    </div>
    
    <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f4f4f5;'>
        <tr>
            <td align='center' style='padding: 32px 16px;'>
                
                <!-- Container -->
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 560px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background-color: #0f172a; padding: 32px 32px 28px 32px; text-align: center;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <p style='margin: 0 0 4px 0; font-size: 22px; font-weight: 700; color: #ffffff; letter-spacing: 2px; font-family: Georgia, ""Times New Roman"", serif;'>ALDA JOYEROS</p>
                                        <p style='margin: 0; font-size: 11px; color: #64748b; letter-spacing: 3px; font-weight: 500;'>DESDE 1962</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Success Message -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 28px 32px; text-align: center;'>
                            <p style='margin: 0 0 6px 0; font-size: 13px; color: rgba(255,255,255,0.85); text-transform: uppercase; letter-spacing: 1px; font-weight: 600;'>Pedido confirmado</p>
                            <p style='margin: 0; font-size: 20px; font-weight: 700; color: #ffffff;'>¡Gracias por tu compra!</p>
                        </td>
                    </tr>
                    
                    <!-- Order Info -->
                    <tr>
                        <td style='padding: 28px 32px 20px 32px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f8fafc; border-radius: 10px;'>
                                <tr>
                                    <td style='padding: 20px;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td width='50%' valign='top'>
                                                    <p style='margin: 0 0 4px 0; font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600;'>Nº Pedido</p>
                                                    <p style='margin: 0; font-size: 22px; font-weight: 700; color: #0f172a;'>#{pedido.Id}</p>
                                                </td>
                                                <td width='50%' align='right' valign='top'>
                                                    <p style='margin: 0 0 4px 0; font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600;'>Fecha</p>
                                                    <p style='margin: 0; font-size: 15px; font-weight: 600; color: #0f172a;'>{pedido.Fecha?.ToString("dd/MM/yyyy")}</p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Products -->
                    <tr>
                        <td style='padding: 0 32px 20px 32px;'>
                            <p style='margin: 0 0 12px 0; font-size: 13px; font-weight: 700; color: #0f172a; text-transform: uppercase; letter-spacing: 0.5px;'>
                                Artículos ({cantidadTotal})
                            </p>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%' style='border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden;'>
                                {productosHtml}
                                <!-- Total -->
                                <tr style='background-color: #f0fdf4;'>
                                    <td style='padding: 16px 24px;'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                            <tr>
                                                <td>
                                                    <p style='margin: 0; font-size: 14px; font-weight: 600; color: #166534;'>Total</p>
                                                    <p style='margin: 2px 0 0 0; font-size: 12px; color: #22c55e;'>Envío gratis</p>
                                                </td>
                                                <td align='right'>
                                                    <p style='margin: 0; font-size: 24px; font-weight: 800; color: #059669;'>{pedido.Total:C}</p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Shipping & Delivery -->
                    <tr>
                        <td style='padding: 0 32px 24px 32px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td width='48%' valign='top'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #f8fafc; border-radius: 10px; border: 1px solid #e2e8f0;'>
                                            <tr>
                                                <td style='padding: 16px;'>
                                                    <p style='margin: 0 0 8px 0; font-size: 11px; font-weight: 700; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px;'>Envío a</p>
                                                    <p style='margin: 0; font-size: 13px; color: #334155; line-height: 1.5;'>{direccionHtml}</p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                    <td width='4%'></td>
                                    <td width='48%' valign='top'>
                                        <table cellpadding='0' cellspacing='0' border='0' width='100%' style='background-color: #eff6ff; border-radius: 10px; border: 1px solid #bfdbfe;'>
                                            <tr>
                                                <td style='padding: 16px;'>
                                                    <p style='margin: 0 0 8px 0; font-size: 11px; font-weight: 700; color: #3b82f6; text-transform: uppercase; letter-spacing: 0.5px;'>Entrega</p>
                                                    <p style='margin: 0; font-size: 18px; font-weight: 700; color: #1e40af;'>3-5 días</p>
                                                    <p style='margin: 2px 0 0 0; font-size: 12px; color: #3b82f6;'>laborables</p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- CTA -->
                    <tr>
                        <td style='padding: 0 32px 32px 32px;' align='center'>
                            <table cellpadding='0' cellspacing='0' border='0'>
                                <tr>
                                    <td style='background-color: #0f172a; border-radius: 8px;'>
                                        <a href='{pedidoUrl}' style='display: inline-block; padding: 14px 36px; font-size: 14px; font-weight: 600; color: #ffffff; text-decoration: none;'>
                                            Ver pedido completo
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Divider -->
                    <tr>
                        <td style='padding: 0 32px;'>
                            <div style='height: 1px; background-color: #e5e7eb;'></div>
                        </td>
                    </tr>
                    
                    <!-- Help -->
                    <tr>
                        <td style='padding: 24px 32px;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <p style='margin: 0 0 12px 0; font-size: 14px; font-weight: 600; color: #0f172a;'>¿Tienes dudas?</p>
                                        <p style='margin: 0; font-size: 13px; color: #64748b;'>
                                            <a href='tel:+34925541227' style='color: #0f172a; text-decoration: none; font-weight: 500;'>925 54 12 27</a>
                                            &nbsp;&middot;&nbsp;
                                            <a href='mailto:aldajoyeros1962@gmail.com' style='color: #0f172a; text-decoration: none; font-weight: 500;'>aldajoyeros1962@gmail.com</a>
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e5e7eb;'>
                            <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                                <tr>
                                    <td align='center'>
                                        <p style='margin: 0 0 4px 0; font-size: 12px; font-weight: 600; color: #64748b;'>ALDA JOYEROS 1962</p>
                                        <p style='margin: 0; font-size: 11px; color: #94a3b8;'>
                                            C/ Sor Livia Alcorta, 24 · 45200 Illescas, Toledo
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    
                </table>
                
                <!-- Unsubscribe -->
                <table cellpadding='0' cellspacing='0' border='0' width='100%' style='max-width: 560px;'>
                    <tr>
                        <td align='center' style='padding: 24px 16px;'>
                            <p style='margin: 0; font-size: 11px; color: #94a3b8;'>
                                &copy; {DateTime.Now.Year} Alda Joyeros. Todos los derechos reservados.
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
                            <p style='margin: 0; font-size: 10px; color: #64748b; letter-spacing: 3px;'>DESDE 1962</p>
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
