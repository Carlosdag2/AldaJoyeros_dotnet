namespace AldaJoyeros.DTOs
{
    /// <summary>
    /// Datos de una factura
    /// </summary>
    public sealed record FacturaDto
    {
        public required string NumeroFactura { get; init; }
        public required DateTime FechaEmision { get; init; }
        public required PedidoDto Pedido { get; init; }
        public required DatosEmpresaDto Empresa { get; init; }
        public required DatosClienteDto Cliente { get; init; }
        
        // Cálculos
        public double BaseImponible => Pedido.Total / 1.21; // IVA 21%
        public double ImporteIva => Pedido.Total - BaseImponible;
        public double Total => Pedido.Total;
    }

    /// <summary>
    /// Datos de la empresa emisora de la factura
    /// </summary>
    public sealed record DatosEmpresaDto
    {
        public required string Nombre { get; init; }
        public required string Cif { get; init; }
        public required string Direccion { get; init; }
        public required string CodigoPostal { get; init; }
        public required string Ciudad { get; init; }
        public required string Provincia { get; init; }
        public required string Telefono { get; init; }
        public required string Email { get; init; }
    }

    /// <summary>
    /// Datos del cliente en la factura
    /// </summary>
    public sealed record DatosClienteDto
    {
        public required string Nombre { get; init; }
        public required string Email { get; init; }
        public string? Nif { get; init; }
        public required string Direccion { get; init; }
        public required string CodigoPostal { get; init; }
        public required string Ciudad { get; init; }
        public required string Provincia { get; init; }
    }
}
