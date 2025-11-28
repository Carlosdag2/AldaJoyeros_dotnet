using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class PedidoRepository : IPedidoRepository
    {
        private readonly AldaJoyerosContext _context;

        public PedidoRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Pedido>> GetAllAsync()
        {
            return await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Direccion)
                .Include(p => p.LineasPedido)
                    .ThenInclude(l => l.Producto)
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();
        }

        public async Task<Pedido?> GetByIdAsync(long id)
        {
            return await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Direccion)
                .Include(p => p.LineasPedido)
                    .ThenInclude(l => l.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Pedido>> GetByUsuarioIdAsync(long usuarioId)
        {
            return await _context.Pedidos
                .Include(p => p.Direccion)
                .Include(p => p.LineasPedido)
                    .ThenInclude(l => l.Producto)
                .Where(p => p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();
        }

        public async Task<Pedido> CreateAsync(Pedido pedido)
        {
            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();
            return pedido;
        }

        public async Task<Pedido> UpdateAsync(Pedido pedido)
        {
            _context.Pedidos.Update(pedido);
            await _context.SaveChangesAsync();
            return pedido;
        }

        public async Task DeleteAsync(long id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido != null)
            {
                _context.Pedidos.Remove(pedido);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.Pedidos.AnyAsync(p => p.Id == id);
        }
    }
}
