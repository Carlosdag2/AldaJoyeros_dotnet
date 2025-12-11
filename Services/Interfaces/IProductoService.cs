using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IProductoService
    {
        /// <summary>
        /// Obtiene todos los productos activos (no eliminados)
        /// </summary>
        Task<IEnumerable<ProductoDto>> GetAllAsync();
        
        /// <summary>
        /// Obtiene todos los productos incluyendo eliminados (para administración)
        /// </summary>
        Task<IEnumerable<ProductoDto>> GetAllIncludingDeletedAsync();
        
        /// <summary>
        /// Obtiene un producto por ID (incluyendo eliminados)
        /// </summary>
        Task<ProductoDto?> GetByIdAsync(long id);
        
        /// <summary>
        /// Obtiene un producto por ID solo si está activo
        /// </summary>
        Task<ProductoDto?> GetByIdActiveAsync(long id);
        
        Task<IEnumerable<ProductoDto>> GetByCategoriaAsync(long categoriaId);
        Task<ProductoDto> CreateAsync(ProductoCreateDto productoCreateDto);
        Task<ProductoDto> UpdateAsync(long id, ProductoUpdateDto productoUpdateDto);
        
        /// <summary>
        /// Soft delete: marca el producto como eliminado
        /// </summary>
        Task DeleteAsync(long id);
        
        /// <summary>
        /// Restaura un producto eliminado
        /// </summary>
        Task RestoreAsync(long id);
        
        /// <summary>
        /// Hard delete: elimina permanentemente el producto y sus imágenes
        /// </summary>
        Task HardDeleteAsync(long id);
    }
}
