using AldaJoyeros.Constants;
using AldaJoyeros.Models;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    /// <summary>
    /// Implementación del servicio de códigos postales españoles.
    /// Principios SOLID aplicados:
    /// - SRP: Solo gestiona la lógica de códigos postales
    /// - OCP: Extensible mediante la interfaz sin modificar la implementación
    /// - LSP: Puede sustituirse por cualquier implementación de ICodigoPostalService
    /// - ISP: Interfaz específica con métodos relacionados
    /// - DIP: Depende de abstracciones (ICodigoPostalService)
    /// </summary>
    public sealed class CodigoPostalService : ICodigoPostalService
    {
        public List<ProvinciaInfo> ObtenerProvincias()
        {
            return CodigoPostalData.PrefijosProvincias
                .Select(p => new ProvinciaInfo { Codigo = p.Key, Nombre = p.Value })
                .OrderBy(p => p.Nombre)
                .ToList();
        }

        public List<string> ObtenerCiudadesPorProvincia(string provincia)
        {
            if (string.IsNullOrWhiteSpace(provincia))
                return [];

            return CodigoPostalData.CiudadesPorProvincia.TryGetValue(provincia, out var ciudades)
                ? ciudades.OrderBy(c => c).ToList()
                : [];
        }

        public CodigoPostalInfo? BuscarPorCodigoPostal(string codigoPostal)
        {
            if (!EsCodigoPostalValido(codigoPostal))
                return null;

            var prefijo = codigoPostal[..2];

            if (!CodigoPostalData.PrefijosProvincias.TryGetValue(prefijo, out var provincia))
                return null;

            var ciudad = ObtenerCiudadParaCodigoPostal(codigoPostal, provincia);

            return new CodigoPostalInfo
            {
                CodigoPostal = codigoPostal,
                Provincia = provincia,
                Ciudad = ciudad
            };
        }

        public bool EsCodigoPostalValido(string codigoPostal)
        {
            if (string.IsNullOrWhiteSpace(codigoPostal))
                return false;

            if (codigoPostal.Length != CodigoPostalData.LongitudCodigoPostal)
                return false;

            if (!codigoPostal.All(char.IsDigit))
                return false;

            var prefijo = codigoPostal[..2];
            return CodigoPostalData.PrefijosProvincias.ContainsKey(prefijo);
        }

        private static string ObtenerCiudadParaCodigoPostal(string codigoPostal, string provincia)
        {
            // Primero buscar en el mapeo específico de códigos postales
            if (CodigoPostalData.CodigosPostalesCiudades.TryGetValue(codigoPostal, out var ciudadEspecifica))
                return ciudadEspecifica;

            // Si no hay ciudad específica, usar la primera de la lista (capital)
            if (CodigoPostalData.CiudadesPorProvincia.TryGetValue(provincia, out var ciudades) && ciudades.Count > 0)
                return ciudades[0];

            // Fallback: usar el nombre de la provincia
            return provincia;
        }
    }
}
