using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IDireccionService
    {
        Task<IEnumerable<DireccionDto>> GetAllAsync();
        Task<DireccionDto?> GetByIdAsync(long id);
        Task<IEnumerable<DireccionDto>> GetByUsuarioIdAsync(long usuarioId);
        Task<DireccionDto> CreateAsync(long usuarioId, DireccionCreateDto direccionCreateDto);
        Task<DireccionDto> UpdateAsync(long id, DireccionUpdateDto direccionUpdateDto);
        Task DeleteAsync(long id);
    }
}
