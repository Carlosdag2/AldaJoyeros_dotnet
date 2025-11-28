using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IProductoService
    {
        Task<IEnumerable<ProductoDto>> GetAllAsync();
        Task<ProductoDto?> GetByIdAsync(long id);
        Task<IEnumerable<ProductoDto>> GetByCategoriaAsync(long categoriaId);
        Task<ProductoDto> CreateAsync(ProductoCreateDto productoCreateDto);
        Task<ProductoDto> UpdateAsync(long id, ProductoUpdateDto productoUpdateDto);
        Task DeleteAsync(long id);
    }
}
