namespace AldaJoyeros.Configuration
{
    /// <summary>
    /// Configuración de datos de la empresa para facturas
    /// </summary>
    public sealed class EmpresaSettings
    {
        public string Nombre { get; set; } = "Alda Joyeros 1962";
        public string Cif { get; set; } = string.Empty;
        public string Direccion { get; set; } = "C/ Sor Livia Alcorta, 24";
        public string CodigoPostal { get; set; } = "45200";
        public string Ciudad { get; set; } = "Illescas";
        public string Provincia { get; set; } = "Toledo";
        public string Telefono { get; set; } = "+34 925 54 12 27";
        public string Email { get; set; } = "aldajoyeros1962@gmail.com";
        public string PrefijoFactura { get; set; } = "AJ";
    }
}
