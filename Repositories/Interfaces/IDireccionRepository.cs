using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IDireccionRepository
    {
        Task<IEnumerable<Direccion>> GetAllAsync();
        Task<Direccion?> GetByIdAsync(long id);
        Task<IEnumerable<Direccion>> GetByUsuarioIdAsync(long usuarioId);
        Task<Direccion> CreateAsync(Direccion direccion);
        Task<Direccion> UpdateAsync(Direccion direccion);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
    }
}
