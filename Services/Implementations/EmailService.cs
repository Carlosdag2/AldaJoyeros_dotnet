using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using AldaJoyeros.Configuration;
using AldaJoyeros.Services.Interfaces;
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
                
                // Intentar con SSL directo (puerto 465) o STARTTLS (puerto 587)
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

        private string GetPasswordResetEmailTemplate(string verifyLink, string code)
        {
            // Separar el código en dígitos individuales para mostrar en cajas
            var codeDigits = code.ToCharArray();
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, Arial, sans-serif; background-color: #f6f8fa;'>
    <table role='presentation' style='width: 100%; border-collapse: collapse;'>
        <tr>
            <td align='center' style='padding: 40px 0;'>
                <table role='presentation' style='width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 40px 40px; text-align: center;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 28px; font-weight: bold;'>💎 ALDA JOYEROS</h1>
                            <p style='color: #d1fae5; margin: 10px 0 0 0; font-size: 14px; letter-spacing: 2px;'>DESDE 1962</p>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='width: 80px; height: 80px; background-color: #ecfdf5; border-radius: 50%; display: inline-block; line-height: 80px; margin-bottom: 20px;'>
                                    <span style='font-size: 40px;'>🔐</span>
                                </div>
                                <h2 style='color: #1f2937; margin: 0 0 10px 0; font-size: 24px;'>Tu código de verificación</h2>
                                <p style='color: #6b7280; margin: 0; font-size: 16px;'>Introduce este código para restablecer tu contraseña</p>
                            </div>
                            
                            <!-- Código de verificación estilo GitHub -->
                            <div style='text-align: center; margin: 40px 0;'>
                                <div style='display: inline-block; background: linear-gradient(135deg, #f0fdf4 0%, #ecfdf5 100%); padding: 20px 30px; border-radius: 16px; border: 2px solid #10b981;'>
                                    <table role='presentation' style='border-collapse: separate; border-spacing: 8px;'>
                                        <tr>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[0]}</td>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[1]}</td>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[2]}</td>
                                            <td style='width: 20px; text-align: center; color: #9ca3af; font-size: 24px;'>-</td>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[3]}</td>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[4]}</td>
                                            <td style='width: 50px; height: 60px; background-color: #ffffff; border: 2px solid #d1fae5; border-radius: 12px; text-align: center; font-size: 32px; font-weight: bold; color: #10b981; font-family: monospace;'>{codeDigits[5]}</td>
                                        </tr>
                                    </table>
                                </div>
                            </div>
                            
                            <p style='color: #4b5563; font-size: 15px; line-height: 1.6; text-align: center; margin-bottom: 30px;'>
                                Haz clic en el botón de abajo y luego introduce el código cuando te lo pidan.
                            </p>
                            
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{verifyLink}' style='display: inline-block; background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: #ffffff; text-decoration: none; padding: 16px 40px; border-radius: 12px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 14px rgba(16, 185, 129, 0.4);'>
                                    Verificar mi identidad
                                </a>
                            </div>
                            
                            <div style='background-color: #fef3c7; border-left: 4px solid #f59e0b; padding: 16px; border-radius: 8px; margin: 30px 0;'>
                                <p style='color: #92400e; margin: 0; font-size: 14px;'>
                                    <strong>⚠️ Importante:</strong> Este código expirará en 1 hora y solo puede usarse una vez. Tienes 5 intentos para introducirlo correctamente.
                                </p>
                            </div>
                            
                            <p style='color: #6b7280; font-size: 14px; line-height: 1.6; margin-top: 20px;'>
                                Si no has solicitado este cambio, puedes ignorar este correo. Tu contraseña permanecerá sin cambios.
                            </p>
                            
                            <p style='color: #9ca3af; font-size: 13px; margin-top: 30px;'>
                                Si el botón no funciona, copia y pega el siguiente enlace en tu navegador:<br>
                                <a href='{verifyLink}' style='color: #10b981; word-break: break-all;'>{verifyLink}</a>
                            </p>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f9fafb; padding: 30px 40px; text-align: center; border-top: 1px solid #e5e7eb;'>
                            <p style='color: #6b7280; margin: 0 0 10px 0; font-size: 14px;'>
                                ¿Necesitas ayuda? Contáctanos en 
                                <a href='mailto:aldajoyeros1962@gmail.com' style='color: #10b981;'>aldajoyeros1962@gmail.com</a>
                            </p>
                            <p style='color: #9ca3af; margin: 0; font-size: 12px;'>
                                © {DateTime.Now.Year} Alda Joyeros 1962. Todos los derechos reservados.<br>
                                Calle Sor Livia Alcorta, 24 - 45200 Illescas, Toledo
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
