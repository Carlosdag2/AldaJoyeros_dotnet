namespace AldaJoyeros.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
        Task SendPasswordResetEmailAsync(string to, string verifyLink, string code);
        Task SendOrderConfirmationEmailAsync(string to, DTOs.PedidoDto pedido);
    }
}
