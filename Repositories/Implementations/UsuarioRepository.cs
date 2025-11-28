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
            return await _context.Usuarios.ToListAsync();
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
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario != null)
            {
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
