using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IProductoRepository
    {
        /// <summary>
        /// Obtiene todos los productos activos (no eliminados)
        /// </summary>
        Task<IEnumerable<Producto>> GetAllAsync();
        
        /// <summary>
        /// Obtiene todos los productos incluyendo eliminados (para administración)
        /// </summary>
        Task<IEnumerable<Producto>> GetAllIncludingDeletedAsync();
        
        /// <summary>
        /// Obtiene un producto por ID (incluyendo eliminados)
        /// </summary>
        Task<Producto?> GetByIdAsync(long id);
        
        /// <summary>
        /// Obtiene un producto por ID solo si está activo
        /// </summary>
        Task<Producto?> GetByIdActiveAsync(long id);
        
        Task<IEnumerable<Producto>> GetByCategoriaAsync(long categoriaId);
        Task<Producto> CreateAsync(Producto producto);
        Task<Producto> UpdateAsync(Producto producto);
        
        /// <summary>
        /// Soft delete: marca el producto como eliminado
        /// </summary>
        Task DeleteAsync(long id);
        
        /// <summary>
        /// Restaura un producto eliminado
        /// </summary>
        Task RestoreAsync(long id);
        
        /// <summary>
        /// Hard delete: elimina permanentemente (usar con precaución)
        /// </summary>
        Task HardDeleteAsync(long id);
        
        Task<bool> ExistsAsync(long id);
        Task<bool> NombreExistsAsync(string nombre);
    }
}
