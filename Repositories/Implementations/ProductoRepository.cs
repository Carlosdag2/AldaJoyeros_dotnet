using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class ProductoRepository : IProductoRepository
    {
        private readonly AldaJoyerosContext _context;

        public ProductoRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtiene todos los productos NO eliminados
        /// </summary>
        public async Task<IEnumerable<Producto>> GetAllAsync()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => !p.Eliminado) // Filtrar eliminados
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene todos los productos incluyendo eliminados (para admin)
        /// </summary>
        public async Task<IEnumerable<Producto>> GetAllIncludingDeletedAsync()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene un producto por ID (incluyendo eliminados para mostrar en pedidos)
        /// </summary>
        public async Task<Producto?> GetByIdAsync(long id)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// Obtiene un producto por ID solo si NO está eliminado
        /// </summary>
        public async Task<Producto?> GetByIdActiveAsync(long id)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id && !p.Eliminado);
        }

        public async Task<IEnumerable<Producto>> GetByCategoriaAsync(long categoriaId)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.CategoriaId == categoriaId && !p.Eliminado) // Filtrar eliminados
                .ToListAsync();
        }

        public async Task<Producto> CreateAsync(Producto producto)
        {
            producto.Eliminado = false;
            producto.FechaEliminado = null;
            
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            
            return (await GetByIdAsync(producto.Id))!;
        }

        public async Task<Producto> UpdateAsync(Producto producto)
        {
            _context.Entry(producto).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            
            return (await GetByIdAsync(producto.Id))!;
        }

        /// <summary>
        /// Soft delete: marca el producto como eliminado en lugar de borrarlo
        /// </summary>
        public async Task DeleteAsync(long id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                producto.Eliminado = true;
                producto.FechaEliminado = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Restaura un producto eliminado
        /// </summary>
        public async Task RestoreAsync(long id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                producto.Eliminado = false;
                producto.FechaEliminado = null;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Eliminación permanente (hard delete) - usar con precaución
        /// </summary>
        public async Task HardDeleteAsync(long id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.Productos.AnyAsync(p => p.Id == id && !p.Eliminado);
        }

        public async Task<bool> NombreExistsAsync(string nombre)
        {
            return await _context.Productos.AnyAsync(p => p.Nombre == nombre && !p.Eliminado);
        }
    }
}
