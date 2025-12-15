namespace AldaJoyeros.Services.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Envía un email simple
        /// </summary>
        Task SendEmailAsync(string to, string subject, string htmlBody);

        /// <summary>
        /// Envía un email con un archivo adjunto
        /// </summary>
        Task SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, byte[]? attachment, string? attachmentName);

        /// <summary>
        /// Envía el email de restablecimiento de contraseña
        /// </summary>
        Task SendPasswordResetEmailAsync(string to, string verifyLink, string code);

        /// <summary>
        /// Envía el email de confirmación de pedido con la factura adjunta
        /// </summary>
        Task SendOrderConfirmationWithInvoiceAsync(string to, DTOs.PedidoDto pedido, byte[] facturaPdf, string numeroFactura);

        /// <summary>
        /// Envía solo la factura por email (para reenvíos)
        /// </summary>
        Task SendInvoiceEmailAsync(string to, DTOs.PedidoDto pedido, byte[] facturaPdf, string numeroFactura);
    }
}
