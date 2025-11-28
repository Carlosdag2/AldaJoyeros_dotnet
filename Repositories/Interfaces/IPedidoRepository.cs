using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IPedidoRepository
    {
        Task<IEnumerable<Pedido>> GetAllAsync();
        Task<Pedido?> GetByIdAsync(long id);
        Task<IEnumerable<Pedido>> GetByUsuarioIdAsync(long usuarioId);
        Task<Pedido> CreateAsync(Pedido pedido);
        Task<Pedido> UpdateAsync(Pedido pedido);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
    }
}
