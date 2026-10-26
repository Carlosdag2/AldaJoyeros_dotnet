namespace AldaJoyeros.DTOs;

public sealed record TraficoMinutoDto(DateTimeOffset Fecha, int Visitantes);
public sealed record TraficoDto(int VisitantesActivos, Dictionary<string, int> VisitantesPorPagina,
    int VistasUltimos30Minutos, int VisitantesUltimos30Minutos, DateTimeOffset ActualizadoEn,
    DateTimeOffset InicioMedicion, int SegundosActividad, IReadOnlyList<TraficoMinutoDto> Historial);
