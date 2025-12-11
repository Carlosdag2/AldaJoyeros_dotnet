using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface ICategoriaRepository
    {
        Task<IEnumerable<Categoria>> GetAllAsync();
        Task<Categoria?> GetByIdAsync(long id);
        Task<Categoria?> GetByNombreAsync(string nombre);
        Task<Categoria> CreateAsync(Categoria categoria);
        Task<Categoria> UpdateAsync(Categoria categoria);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task<bool> NombreExistsAsync(string nombre);
        
        /// <summary>
        /// Obtiene o crea la categoría "Sin categoría" para productos huérfanos
        /// </summary>
        Task<Categoria> GetOrCreateDefaultCategoryAsync();
        
        /// <summary>
        /// Reasigna todos los productos de una categoría a otra
        /// </summary>
        Task ReassignProductsAsync(long fromCategoriaId, long toCategoriaId);
    }
}
