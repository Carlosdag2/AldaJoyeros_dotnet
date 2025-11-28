using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface ICarritoRepository
    {
        Task<IEnumerable<CarritoItem>> GetByUsuarioIdAsync(long usuarioId);
        Task<CarritoItem?> GetByIdAsync(long id);
        Task<CarritoItem?> GetByUsuarioAndProductoAsync(long usuarioId, long productoId);
        Task<CarritoItem> CreateAsync(CarritoItem carritoItem);
        Task<CarritoItem> UpdateAsync(CarritoItem carritoItem);
        Task DeleteAsync(long id);
        Task DeleteByUsuarioIdAsync(long usuarioId);
        Task<bool> ExistsAsync(long id);
    }
}
