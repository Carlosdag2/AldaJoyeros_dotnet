namespace AldaJoyeros.Configuration
{
    /// <summary>
    /// Configuración de Stripe para pagos
    /// </summary>
    public class StripeSettings
    {
        /// <summary>
        /// Clave pública de Stripe (pk_test_... o pk_live_...)
        /// </summary>
        public string PublishableKey { get; set; } = string.Empty;

        /// <summary>
        /// Clave secreta de Stripe (sk_test_... o sk_live_...)
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Webhook secret para validar eventos de Stripe
        /// </summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// Indica si está en modo test
        /// </summary>
        public bool IsTestMode => PublishableKey.StartsWith("pk_test");
    }
}
