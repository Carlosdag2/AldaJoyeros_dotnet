namespace AldaJoyeros.Services.Interfaces
{
    /// <summary>
    /// Servicio para procesamiento de pagos
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Crea un PaymentIntent en Stripe
        /// </summary>
        /// <param name="amount">Monto en la moneda especificada</param>
        /// <param name="currency">Código de moneda (EUR, USD, etc.)</param>
        /// <param name="description">Descripción del pago</param>
        /// <param name="metadata">Metadata adicional para el pago</param>
        /// <returns>Resultado con ClientSecret y PaymentIntentId</returns>
        Task<PaymentIntentResult> CreatePaymentIntentAsync(
            decimal amount, 
            string currency = "EUR",
            string? description = null,
            Dictionary<string, string>? metadata = null);

        /// <summary>
        /// Verifica el estado de un PaymentIntent
        /// </summary>
        Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentIntentId);

        /// <summary>
        /// Confirma un pago después de recibir confirmación del cliente
        /// </summary>
        Task<bool> ConfirmPaymentAsync(string paymentIntentId);

        /// <summary>
        /// Cancela un PaymentIntent
        /// </summary>
        Task<bool> CancelPaymentAsync(string paymentIntentId);

        /// <summary>
        /// Obtiene la clave pública de Stripe para el frontend
        /// </summary>
        string GetPublishableKey();
    }

    public class PaymentIntentResult
    {
        public bool Success { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? ClientSecret { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PaymentStatusResult
    {
        public bool Found { get; set; }
        public string Status { get; set; } = "unknown";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public bool IsSucceeded => Status == "succeeded";
        public bool IsPending => Status == "processing" || Status == "requires_confirmation";
        public bool IsFailed => Status == "canceled" || Status == "requires_payment_method";
    }
}
