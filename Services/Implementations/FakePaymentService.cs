using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class FakePaymentService : IPaymentService
    {
        private readonly Dictionary<string, (decimal Amount, string Status, string Currency)> _payments = new();
        private readonly Random _random = new();

        public Task<string> CreatePaymentIntentAsync(decimal amount, string currency = "EUR")
        {
            // Generar un ID único para el pago
            var paymentIntentId = $"pi_fake_{Guid.NewGuid():N}";
            
            // Guardar el pago pendiente
            _payments[paymentIntentId] = (amount, "pending", currency);
            
            return Task.FromResult(paymentIntentId);
        }

        public Task<bool> ProcessPaymentAsync(string paymentIntentId, string metodoPago)
        {
            if (!_payments.ContainsKey(paymentIntentId))
            {
                return Task.FromResult(false);
            }

            // Simular un procesamiento de pago
            // En el 95% de los casos el pago es exitoso
            var success = _random.Next(100) < 95;

            if (success)
            {
                var payment = _payments[paymentIntentId];
                _payments[paymentIntentId] = (payment.Amount, "succeeded", payment.Currency);
            }
            else
            {
                var payment = _payments[paymentIntentId];
                _payments[paymentIntentId] = (payment.Amount, "failed", payment.Currency);
            }

            return Task.FromResult(success);
        }

        public Task<string> GetPaymentStatusAsync(string paymentIntentId)
        {
            if (!_payments.ContainsKey(paymentIntentId))
            {
                return Task.FromResult("not_found");
            }

            return Task.FromResult(_payments[paymentIntentId].Status);
        }
    }
}
