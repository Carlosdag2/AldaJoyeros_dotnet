using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface ICarritoService
    {
        Task<IEnumerable<CarritoItemDto>> GetByUsuarioIdAsync(long usuarioId);
        Task<CarritoItemDto> AddItemAsync(long usuarioId, CarritoItemCreateDto carritoItemCreateDto);
        Task<CarritoItemDto> UpdateItemAsync(long usuarioId, long itemId, CarritoItemUpdateDto carritoItemUpdateDto);
        Task DeleteItemAsync(long usuarioId, long itemId);
        Task ClearCarritoAsync(long usuarioId);
        Task<double> GetTotalAsync(long usuarioId);
        Task<int> GetTotalItemsAsync(long usuarioId);
    }
}
