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
            var subject = "Código de verificación - Alda Joyeros 1962";
            var htmlBody = GetPasswordResetEmailTemplate(verifyLink, code);
            await SendEmailAsync(to, subject, htmlBody);
        }

        public async Task SendOrderConfirmationWithInvoiceAsync(string to, PedidoDto pedido, byte[] facturaPdf, string numeroFactura)
        {
            var subject = $"Confirmación de pedido #{pedido.Id} - Alda Joyeros 1962";
            var htmlBody = GetOrderConfirmationWithInvoiceEmailTemplate(pedido, numeroFactura);
            var attachmentName = $"Factura_{numeroFactura.Replace("/", "-")}.pdf";
            
            await SendEmailWithAttachmentAsync(to, subject, htmlBody, facturaPdf, attachmentName);
        }

        public async Task SendInvoiceEmailAsync(string to, PedidoDto pedido, byte[] facturaPdf, string numeroFactura)
        {
            var subject = $"Factura {numeroFactura} - Alda Joyeros 1962";
            var htmlBody = GetInvoiceEmailTemplate(pedido, numeroFactura);
            var attachmentName = $"Factura_{numeroFactura.Replace("/", "-")}.pdf";
            
            await SendEmailWithAttachmentAsync(to, subject, htmlBody, facturaPdf, attachmentName);
        }

        public async Task SendNewOrderNotificationAsync(PedidoDto pedido, string customerEmail, string paymentMethod)
        {
            var recipient = string.IsNullOrWhiteSpace(_emailSettings.OwnerEmail)
                ? _emailSettings.SenderEmail
                : _emailSettings.OwnerEmail;
            var subject = $"Nuevo pedido #{pedido.Id} - Alda Joyeros 1962";
            var htmlBody = EmailTemplates.NewOrder(pedido, customerEmail, paymentMethod, _emailSettings.BaseUrl);
            await SendEmailAsync(recipient, subject, htmlBody);
        }

        private string GetOrderConfirmationWithInvoiceEmailTemplate(PedidoDto pedido, string numeroFactura)
            => EmailTemplates.Order(pedido, numeroFactura, _emailSettings.BaseUrl);

        private string GetInvoiceEmailTemplate(PedidoDto pedido, string numeroFactura)
            => EmailTemplates.Invoice(pedido, numeroFactura, _emailSettings.BaseUrl);

        private string GetPasswordResetEmailTemplate(string verifyLink, string code)
            => EmailTemplates.Password(verifyLink, code);
    }
}
