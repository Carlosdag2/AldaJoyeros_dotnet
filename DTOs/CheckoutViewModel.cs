namespace AldaJoyeros.DTOs
{
    public class CheckoutViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public DireccionDto Direccion { get; set; } = new DireccionDto();
        public string MetodoPago { get; set; } = "Contra Reembolso";
        public decimal Total => (decimal)CarritoItems.Sum(item => item.Subtotal);
        
        // Stripe
        public string? StripePublishableKey { get; set; }
        public string? ClientSecret { get; set; }
        public string? PaymentIntentId { get; set; }
    }
}
