using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IProductoImagenService
    {
        Task<IEnumerable<ProductoImagenDto>> GetByProductoIdAsync(long productoId);
        Task<ProductoImagenDto?> GetByIdAsync(long id);
        Task<ProductoImagenDto> CreateAsync(ProductoImagenCreateDto imagenDto);
        Task<ProductoImagenDto> CreateFromFileAsync(ProductoImagenUploadDto uploadDto);
        Task DeleteAsync(long id);
        Task DeleteByStringIdAsync(string id);
        Task SetAsPrincipalAsync(string id);
    }
}
