namespace AldaJoyeros.Configuration
{
    /// <summary>
    /// Configuración de datos de la empresa para facturas
    /// </summary>
    public sealed class EmpresaSettings
    {
        private string nombre = AldaJoyeros.Helpers.StoreBrand.Name;
        public string Nombre { get => AldaJoyeros.Helpers.StoreBrand.NormalizeName(nombre); set => nombre = value; }
        // Cif se mantiene para admitir la configuración existente; el identificador público es NIF.
        public const string DefaultNif = "52974088M";
        public string Cif { get; set; } = string.Empty;
        public string Nif => string.IsNullOrWhiteSpace(Cif) ? DefaultNif : Cif.Trim();
        public string Direccion { get; set; } = "C/ Sor Livia Alcorta, 24";
        public string CodigoPostal { get; set; } = "45200";
        public string Ciudad { get; set; } = "Illescas";
        public string Provincia { get; set; } = "Toledo";
        public string Telefono { get; set; } = "+34 925 54 12 27";
        public string Email { get; set; } = "aldajoyeros1962@gmail.com";
        public string PrefijoFactura { get; set; } = "AJ";
    }
}
