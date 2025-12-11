using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly AldaJoyerosContext _context;
        private const string DEFAULT_CATEGORY_NAME = "Sin categoría";

        public CategoriaRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> GetAllAsync()
        {
            return await _context.Categorias
                .Include(c => c.Productos.Where(p => !p.Eliminado)) // Solo contar productos activos
                .ToListAsync();
        }

        public async Task<Categoria?> GetByIdAsync(long id)
        {
            return await _context.Categorias
                .Include(c => c.Productos.Where(p => !p.Eliminado))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Categoria?> GetByNombreAsync(string nombre)
        {
            return await _context.Categorias
                .Include(c => c.Productos.Where(p => !p.Eliminado))
                .FirstOrDefaultAsync(c => c.Nombre == nombre);
        }

        public async Task<Categoria> CreateAsync(Categoria categoria)
        {
            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();
            return categoria;
        }

        public async Task<Categoria> UpdateAsync(Categoria categoria)
        {
            _context.Categorias.Update(categoria);
            await _context.SaveChangesAsync();
            return categoria;
        }

        public async Task DeleteAsync(long id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria != null)
            {
                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.Categorias.AnyAsync(c => c.Id == id);
        }

        public async Task<bool> NombreExistsAsync(string nombre)
        {
            return await _context.Categorias.AnyAsync(c => c.Nombre == nombre);
        }

        /// <summary>
        /// Obtiene o crea la categoría "Sin categoría" para productos huérfanos
        /// </summary>
        public async Task<Categoria> GetOrCreateDefaultCategoryAsync()
        {
            var defaultCategory = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Nombre == DEFAULT_CATEGORY_NAME);

            if (defaultCategory == null)
            {
                defaultCategory = new Categoria
                {
                    Nombre = DEFAULT_CATEGORY_NAME
                };
                _context.Categorias.Add(defaultCategory);
                await _context.SaveChangesAsync();
            }

            return defaultCategory;
        }

        /// <summary>
        /// Reasigna todos los productos de una categoría a otra
        /// </summary>
        public async Task ReassignProductsAsync(long fromCategoriaId, long toCategoriaId)
        {
            var productos = await _context.Productos
                .Where(p => p.CategoriaId == fromCategoriaId)
                .ToListAsync();

            foreach (var producto in productos)
            {
                producto.CategoriaId = toCategoriaId;
            }

            await _context.SaveChangesAsync();
        }
    }
}
