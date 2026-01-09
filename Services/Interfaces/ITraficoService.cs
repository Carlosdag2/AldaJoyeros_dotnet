namespace AldaJoyeros.Services.Interfaces
{
    public interface ITraficoService
    {
        void RegistrarVisita(string sessionId, string pagina);
        int ObtenerVisitantesActivos();
        Dictionary<string, int> ObtenerVisitantesPorPagina();
        void LimpiarSesionesExpiradas();
    }
}
