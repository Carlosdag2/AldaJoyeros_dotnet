using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface ICategoriaRepository
    {
        Task<IEnumerable<Categoria>> GetAllAsync();
        Task<Categoria?> GetByIdAsync(long id);
        Task<Categoria> CreateAsync(Categoria categoria);
        Task<Categoria> UpdateAsync(Categoria categoria);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task<bool> NombreExistsAsync(string nombre);
    }
}
