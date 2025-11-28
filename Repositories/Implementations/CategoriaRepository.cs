using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly AldaJoyerosContext _context;

        public CategoriaRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> GetAllAsync()
        {
            return await _context.Categorias.ToListAsync();
        }

        public async Task<Categoria?> GetByIdAsync(long id)
        {
            return await _context.Categorias
                .Include(c => c.Productos)
                .FirstOrDefaultAsync(c => c.Id == id);
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
    }
}
