namespace AldaJoyeros.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreatePaymentIntentAsync(decimal amount, string currency = "EUR");
        Task<bool> ProcessPaymentAsync(string paymentIntentId, string metodoPago);
        Task<string> GetPaymentStatusAsync(string paymentIntentId);
    }
}
