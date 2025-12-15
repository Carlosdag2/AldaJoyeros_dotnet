using AldaJoyeros.Models;

namespace AldaJoyeros.Services.Interfaces
{
    /// <summary>
    /// Servicio para gestionar información de códigos postales españoles
    /// </summary>
    public interface ICodigoPostalService
    {
        /// <summary>
        /// Obtiene la lista de todas las provincias de España
        /// </summary>
        List<ProvinciaInfo> ObtenerProvincias();

        /// <summary>
        /// Obtiene las ciudades de una provincia específica
        /// </summary>
        /// <param name="provincia">Nombre de la provincia</param>
        List<string> ObtenerCiudadesPorProvincia(string provincia);

        /// <summary>
        /// Busca información de ubicación por código postal
        /// </summary>
        /// <param name="codigoPostal">Código postal de 5 dígitos</param>
        CodigoPostalInfo? BuscarPorCodigoPostal(string codigoPostal);

        /// <summary>
        /// Valida si un código postal tiene formato válido
        /// </summary>
        /// <param name="codigoPostal">Código postal a validar</param>
        bool EsCodigoPostalValido(string codigoPostal);
    }
}
