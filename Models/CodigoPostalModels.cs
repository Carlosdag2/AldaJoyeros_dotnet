namespace AldaJoyeros.Models
{
    /// <summary>
    /// Información de una provincia española
    /// </summary>
    public sealed record ProvinciaInfo
    {
        public required string Codigo { get; init; }
        public required string Nombre { get; init; }
    }

    /// <summary>
    /// Información de ubicación asociada a un código postal
    /// </summary>
    public sealed record CodigoPostalInfo
    {
        public required string CodigoPostal { get; init; }
        public required string Provincia { get; init; }
        public required string Ciudad { get; init; }
    }
}
