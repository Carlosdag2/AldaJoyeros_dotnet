using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly AldaJoyerosContext _context;

        public UsuarioRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Usuario>> GetAllAsync()
        {
            return await _context.Usuarios
                .Include(u => u.Pedidos)
                .ToListAsync();
        }

        public async Task<Usuario?> GetByIdAsync(long id)
        {
            return await _context.Usuarios
                .Include(u => u.Direcciones)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<Usuario?> GetByEmailAsync(string email)
        {
            return await _context.Usuarios
                .Include(u => u.Direcciones)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<Usuario> CreateAsync(Usuario usuario)
        {
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            return usuario;
        }

        public async Task<Usuario> UpdateAsync(Usuario usuario)
        {
            _context.Entry(usuario).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return usuario;
        }

        public async Task DeleteAsync(long id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.CarritoItems)
                .Include(u => u.Direcciones)
                .Include(u => u.Pedidos)
                    .ThenInclude(p => p.LineasPedido)
                .FirstOrDefaultAsync(u => u.Id == id);
                
            if (usuario != null)
            {
                // Eliminar items del carrito
                if (usuario.CarritoItems != null && usuario.CarritoItems.Any())
                {
                    _context.CarritoItems.RemoveRange(usuario.CarritoItems);
                }

                // Eliminar líneas de pedido y pedidos
                if (usuario.Pedidos != null && usuario.Pedidos.Any())
                {
                    foreach (var pedido in usuario.Pedidos)
                    {
                        if (pedido.LineasPedido != null && pedido.LineasPedido.Any())
                        {
                            _context.LineasPedido.RemoveRange(pedido.LineasPedido);
                        }
                    }
                    _context.Pedidos.RemoveRange(usuario.Pedidos);
                }

                // Eliminar direcciones
                if (usuario.Direcciones != null && usuario.Direcciones.Any())
                {
                    _context.Direcciones.RemoveRange(usuario.Direcciones);
                }

                // Eliminar tokens de reset de contraseña
                var tokens = await _context.PasswordResetTokens
                    .Where(t => t.UserId == id)
                    .ToListAsync();
                if (tokens.Any())
                {
                    _context.PasswordResetTokens.RemoveRange(tokens);
                }

                // Finalmente eliminar el usuario
                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.Usuarios.AnyAsync(u => u.Id == id);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Usuarios.AnyAsync(u => u.Email == email);
        }
    }
}
