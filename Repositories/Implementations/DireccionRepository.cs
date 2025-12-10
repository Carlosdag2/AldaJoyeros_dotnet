using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    public class DireccionRepository : IDireccionRepository
    {
        private readonly AldaJoyerosContext _context;

        public DireccionRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Direccion>> GetAllAsync()
        {
            return await _context.Direcciones.ToListAsync();
        }

        public async Task<Direccion?> GetByIdAsync(long id)
        {
            return await _context.Direcciones.FindAsync(id);
        }

        public async Task<IEnumerable<Direccion>> GetByUsuarioIdAsync(long usuarioId)
        {
            return await _context.Direcciones
                .Where(d => d.UsuarioId == usuarioId)
                .OrderByDescending(d => d.Id)
                .ToListAsync();
        }

        public async Task<Direccion> CreateAsync(Direccion direccion)
        {
            _context.Direcciones.Add(direccion);
            await _context.SaveChangesAsync();
            return direccion;
        }

        public async Task<Direccion> UpdateAsync(Direccion direccion)
        {
            _context.Direcciones.Update(direccion);
            await _context.SaveChangesAsync();
            return direccion;
        }

        public async Task DeleteAsync(long id)
        {
            var direccion = await _context.Direcciones.FindAsync(id);
            if (direccion != null)
            {
                _context.Direcciones.Remove(direccion);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.Direcciones.AnyAsync(d => d.Id == id);
        }
    }
}
