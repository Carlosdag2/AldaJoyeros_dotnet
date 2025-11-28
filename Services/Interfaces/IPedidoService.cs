using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IPedidoService
    {
        Task<IEnumerable<PedidoDto>> GetAllAsync();
        Task<PedidoDto?> GetByIdAsync(long id);
        Task<IEnumerable<PedidoDto>> GetByUsuarioIdAsync(long usuarioId);
        Task<PedidoDto> CreateFromCarritoAsync(long usuarioId, PedidoCreateDto pedidoCreateDto);
        Task<PedidoDto> UpdateEstadoAsync(long id, PedidoUpdateEstadoDto pedidoUpdateEstadoDto);
        Task DeleteAsync(long id);
    }
}
