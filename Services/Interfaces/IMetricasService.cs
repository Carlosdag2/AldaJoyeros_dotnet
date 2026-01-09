using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IMetricasService
    {
        Task<MetricasDto> GetMetricasAsync();
    }
}
