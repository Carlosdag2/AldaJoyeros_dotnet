using System.ComponentModel.DataAnnotations;

namespace AldaJoyeros.DTOs
{
    /// <summary>
    /// ViewModel principal del checkout (datos compartidos entre pasos)
    /// </summary>
    public class CheckoutViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public decimal Total => (decimal)CarritoItems.Sum(item => item.Subtotal);
        public int TotalItems => CarritoItems.Sum(item => item.Cantidad);
    }

    /// <summary>
    /// Paso 1: Dirección de envío
    /// </summary>
    public class CheckoutDireccionViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public decimal Total => (decimal)CarritoItems.Sum(item => item.Subtotal);

        // Direcciones guardadas del usuario
        public IEnumerable<DireccionDto> DireccionesGuardadas { get; set; } = new List<DireccionDto>();

        // Dirección seleccionada (si usa una guardada)
        public long? DireccionSeleccionadaId { get; set; }

        // Nueva dirección (si crea una nueva)
        public DireccionCreateDto NuevaDireccion { get; set; } = new DireccionCreateDto();

        // Indica si usa dirección nueva o existente
        public bool UsarNuevaDireccion { get; set; } = true;

        // Guardar la nueva dirección para futuras compras
        public bool GuardarDireccion { get; set; } = false;
    }

    /// <summary>
    /// Paso 2: Método de pago
    /// </summary>
    public class CheckoutPagoViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public decimal Total => (decimal)CarritoItems.Sum(item => item.Subtotal);

        // Dirección confirmada del paso anterior
        public DireccionDto Direccion { get; set; } = new DireccionDto();

        // Método de pago seleccionado
        [Required(ErrorMessage = "Selecciona un método de pago")]
        public string MetodoPago { get; set; } = "Contra Reembolso";

        // Stripe
        public string? StripePublishableKey { get; set; }
        public string? ClientSecret { get; set; }
        public string? PaymentIntentId { get; set; }
    }

    /// <summary>
    /// Paso 3: Confirmación final
    /// </summary>
    public class CheckoutConfirmacionViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public decimal Total => (decimal)CarritoItems.Sum(item => item.Subtotal);

        public DireccionDto Direccion { get; set; } = new DireccionDto();
        public string MetodoPago { get; set; } = string.Empty;
        public string? PaymentIntentId { get; set; }

        // Para mostrar info del pago si es con tarjeta
        public bool PagoConTarjeta => MetodoPago == "Tarjeta";
        public string MetodoPagoDisplay => MetodoPago == "Tarjeta" ? "Tarjeta de Crédito/Débito" : MetodoPago;
    }

    /// <summary>
    /// Datos de sesión del checkout (para mantener estado entre pasos)
    /// </summary>
    public class CheckoutSessionData
    {
        public DireccionDto? Direccion { get; set; }
        public string? MetodoPago { get; set; }
        public string? PaymentIntentId { get; set; }
        public bool DireccionConfirmada { get; set; }
        public bool PagoConfirmado { get; set; }
    }
}
