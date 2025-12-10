using AldaJoyeros.Configuration;
using AldaJoyeros.Services.Interfaces;
using Microsoft.Extensions.Options;
using Stripe;

namespace AldaJoyeros.Services.Implementations
{
    /// <summary>
    /// Implementación de pagos usando Stripe API
    /// En modo test, usa tarjetas de prueba de Stripe
    /// </summary>
    public class StripePaymentService : IPaymentService
    {
        private readonly StripeSettings _stripeSettings;
        private readonly ILogger<StripePaymentService> _logger;

        public StripePaymentService(
            IOptions<StripeSettings> stripeSettings,
            ILogger<StripePaymentService> logger)
        {
            _stripeSettings = stripeSettings.Value;
            _logger = logger;

            // Configurar la API key de Stripe
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        public string GetPublishableKey() => _stripeSettings.PublishableKey;

        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
            decimal amount,
            string currency = "EUR",
            string? description = null,
            Dictionary<string, string>? metadata = null)
        {
            try
            {
                // Stripe espera el monto en céntimos (para EUR)
                var amountInCents = (long)(amount * 100);

                var options = new PaymentIntentCreateOptions
                {
                    Amount = amountInCents,
                    Currency = currency.ToLower(),
                    Description = description ?? "Compra en Alda Joyeros",
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                    },
                    Metadata = metadata ?? new Dictionary<string, string>()
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                _logger.LogInformation(
                    "PaymentIntent creado: {PaymentIntentId} por {Amount} {Currency}",
                    paymentIntent.Id, amount, currency);

                return new PaymentIntentResult
                {
                    Success = true,
                    PaymentIntentId = paymentIntent.Id,
                    ClientSecret = paymentIntent.ClientSecret
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error al crear PaymentIntent en Stripe");
                return new PaymentIntentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                return new PaymentStatusResult
                {
                    Found = true,
                    Status = paymentIntent.Status,
                    Amount = paymentIntent.Amount / 100m, // Convertir de céntimos
                    Currency = paymentIntent.Currency.ToUpper()
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error al obtener estado del PaymentIntent {Id}", paymentIntentId);
                return new PaymentStatusResult { Found = false };
            }
        }

        public async Task<bool> ConfirmPaymentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.ConfirmAsync(paymentIntentId);
                
                return paymentIntent.Status == "succeeded";
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error al confirmar PaymentIntent {Id}", paymentIntentId);
                return false;
            }
        }

        public async Task<bool> CancelPaymentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                await service.CancelAsync(paymentIntentId);
                return true;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error al cancelar PaymentIntent {Id}", paymentIntentId);
                return false;
            }
        }
    }
}
