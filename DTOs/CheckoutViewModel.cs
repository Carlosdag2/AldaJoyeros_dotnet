namespace AldaJoyeros.DTOs
{
    public class CheckoutViewModel
    {
        public IEnumerable<CarritoItemDto> CarritoItems { get; set; } = new List<CarritoItemDto>();
        public DireccionDto Direccion { get; set; } = new DireccionDto();
        public string MetodoPago { get; set; } = "Contra Reembolso";
        public double Total => CarritoItems.Sum(item => item.Subtotal);
    }
}
