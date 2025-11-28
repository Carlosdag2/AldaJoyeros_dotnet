using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class CarritoRepository : ICarritoRepository
    {
        private readonly AldaJoyerosContext _context;

        public CarritoRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CarritoItem>> GetByUsuarioIdAsync(long usuarioId)
        {
            return await _context.CarritoItems
                .Include(c => c.Producto)
                    .ThenInclude(p => p.Categoria)
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();
        }

        public async Task<CarritoItem?> GetByIdAsync(long id)
        {
            return await _context.CarritoItems
                .Include(c => c.Producto)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<CarritoItem?> GetByUsuarioAndProductoAsync(long usuarioId, long productoId)
        {
            return await _context.CarritoItems
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.ProductoId == productoId);
        }

        public async Task<CarritoItem> CreateAsync(CarritoItem carritoItem)
        {
            _context.CarritoItems.Add(carritoItem);
            await _context.SaveChangesAsync();
            return carritoItem;
        }

        public async Task<CarritoItem> UpdateAsync(CarritoItem carritoItem)
        {
            _context.CarritoItems.Update(carritoItem);
            await _context.SaveChangesAsync();
            return carritoItem;
        }

        public async Task DeleteAsync(long id)
        {
            var carritoItem = await _context.CarritoItems.FindAsync(id);
            if (carritoItem != null)
            {
                _context.CarritoItems.Remove(carritoItem);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByUsuarioIdAsync(long usuarioId)
        {
            var items = await _context.CarritoItems
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();
            
            _context.CarritoItems.RemoveRange(items);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.CarritoItems.AnyAsync(c => c.Id == id);
        }
    }
}
