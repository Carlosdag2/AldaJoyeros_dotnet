using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<IEnumerable<CategoriaDto>> GetAllAsync();
        Task<CategoriaDto?> GetByIdAsync(long id);
        Task<CategoriaDto> CreateAsync(CategoriaCreateDto categoriaCreateDto);
        Task<CategoriaDto> UpdateAsync(long id, CategoriaUpdateDto categoriaUpdateDto);
        Task DeleteAsync(long id);
    }
}
