namespace AldaJoyeros.Services.Interfaces
{
    public interface ITraficoService
    {
        void RegistrarVisita(string sessionId, string pagina);
        int ObtenerVisitantesActivos();
        Dictionary<string, int> ObtenerVisitantesPorPagina();
        void LimpiarSesionesExpiradas();
        void RegistrarActividad(string visitante, string pestana, string pagina, long secuencia = 0, bool visible = true);
        void CerrarPestana(string visitante, string pestana, long secuencia = 0);
        void RetirarConsentimiento(string visitante);
        AldaJoyeros.DTOs.TraficoDto ObtenerSnapshot();
    }
}
