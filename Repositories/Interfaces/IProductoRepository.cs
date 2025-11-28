using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IProductoRepository
    {
        Task<IEnumerable<Producto>> GetAllAsync();
        Task<Producto?> GetByIdAsync(long id);
        Task<IEnumerable<Producto>> GetByCategoriaAsync(long categoriaId);
        Task<Producto> CreateAsync(Producto producto);
        Task<Producto> UpdateAsync(Producto producto);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task<bool> NombreExistsAsync(string nombre);
    }
}
