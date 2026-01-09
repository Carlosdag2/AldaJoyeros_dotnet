using System.Collections.Concurrent;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class TraficoService : ITraficoService
    {
        // Almacena sessionId -> (última actividad, página actual)
        private readonly ConcurrentDictionary<string, (DateTime UltimaActividad, string Pagina)> _visitantes = new();
        
        // Tiempo en minutos para considerar una sesión como activa
        private const int MINUTOS_SESION_ACTIVA = 5;

        public void RegistrarVisita(string sessionId, string pagina)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            
            _visitantes.AddOrUpdate(
                sessionId,
                (DateTime.Now, pagina),
                (key, old) => (DateTime.Now, pagina)
            );
            
            // Limpiar sesiones expiradas periódicamente
            if (_visitantes.Count > 100)
            {
                LimpiarSesionesExpiradas();
            }
        }

        public int ObtenerVisitantesActivos()
        {
            var limite = DateTime.Now.AddMinutes(-MINUTOS_SESION_ACTIVA);
            return _visitantes.Count(v => v.Value.UltimaActividad >= limite);
        }

        public Dictionary<string, int> ObtenerVisitantesPorPagina()
        {
            var limite = DateTime.Now.AddMinutes(-MINUTOS_SESION_ACTIVA);
            
            return _visitantes
                .Where(v => v.Value.UltimaActividad >= limite)
                .GroupBy(v => SimplificarPagina(v.Value.Pagina))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public void LimpiarSesionesExpiradas()
        {
            var limite = DateTime.Now.AddMinutes(-MINUTOS_SESION_ACTIVA * 2);
            
            var sesionesExpiradas = _visitantes
                .Where(v => v.Value.UltimaActividad < limite)
                .Select(v => v.Key)
                .ToList();

            foreach (var sessionId in sesionesExpiradas)
            {
                _visitantes.TryRemove(sessionId, out _);
            }
        }

        private string SimplificarPagina(string pagina)
        {
            if (string.IsNullOrEmpty(pagina) || pagina == "/")
                return "Inicio";
            
            pagina = pagina.ToLower().TrimStart('/');
            
            if (pagina.StartsWith("productos/detalle"))
                return "Detalle Producto";
            if (pagina.StartsWith("productos"))
                return "Catálogo";
            if (pagina.StartsWith("carrito"))
                return "Carrito";
            if (pagina.StartsWith("checkout"))
                return "Checkout";
            if (pagina.StartsWith("auth"))
                return "Login/Registro";
            if (pagina.StartsWith("cuenta"))
                return "Mi Cuenta";
            if (pagina.StartsWith("admin"))
                return "Panel Admin";
            if (pagina.StartsWith("pedidos"))
                return "Pedidos";
            
            return "Otras";
        }
    }
}
