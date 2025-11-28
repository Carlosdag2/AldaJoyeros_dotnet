using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<IEnumerable<Usuario>> GetAllAsync();
        Task<Usuario?> GetByIdAsync(long id);
        Task<Usuario?> GetByEmailAsync(string email);
        Task<Usuario> CreateAsync(Usuario usuario);
        Task<Usuario> UpdateAsync(Usuario usuario);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task<bool> EmailExistsAsync(string email);
    }
}
