using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IMetricasService
    {
        Task<MetricasDto> GetMetricasAsync(int dias = 30);
    }
}
