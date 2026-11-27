using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Mappings;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly AppMapper _mapper;
        private readonly AldaJoyeros.Data.AldaJoyerosContext? _sql;
        private readonly bool _optimized;

        public UsuarioService(IUsuarioRepository usuarioRepository, AppMapper mapper, AldaJoyeros.Data.AldaJoyerosContext? sql = null, Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.StorageOptimizationOptions>? optimization = null)
        {
            _usuarioRepository = usuarioRepository;
            _mapper = mapper; _sql = sql; _optimized = optimization?.Value.Enabled ?? true;
        }

        public async Task<IEnumerable<UsuarioDto>> GetAllAsync()
        {
            if (_optimized && _sql != null) return await _sql.Usuarios.AsNoTracking().Select(u => new UsuarioDto {
                Id = u.Id, Email = u.Email, Rol = u.Rol, CantidadPedidos = u.Pedidos.Count
            }).ToListAsync();
            var usuarios = await _usuarioRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<UsuarioDto>>(usuarios);
        }

        public async Task<UsuarioDto?> GetByIdAsync(long id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            return usuario == null ? null : _mapper.Map<UsuarioDto>(usuario);
        }

        public async Task<UsuarioDto?> GetByEmailAsync(string email)
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(email);
            return usuario == null ? null : _mapper.Map<UsuarioDto>(usuario);
        }

        public async Task<UsuarioDto> CreateAsync(UsuarioCreateDto usuarioCreateDto)
        {
            if (await _usuarioRepository.EmailExistsAsync(usuarioCreateDto.Email))
            {
                throw new InvalidOperationException("El email ya está registrado");
            }

            var usuario = _mapper.Map<Usuario>(usuarioCreateDto);
            usuario.Password = BCrypt.Net.BCrypt.HashPassword(usuarioCreateDto.Password);
            
            var createdUsuario = await _usuarioRepository.CreateAsync(usuario);
            return _mapper.Map<UsuarioDto>(createdUsuario);
        }

        public async Task<UsuarioDto> UpdateAsync(long id, UsuarioUpdateDto usuarioUpdateDto)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
            {
                throw new KeyNotFoundException("Usuario no encontrado");
            }

            // Actualizar email y rol
            usuario.Email = usuarioUpdateDto.Email;
            usuario.Rol = usuarioUpdateDto.Rol;

            // Solo actualizar contrase�a si se proporciona una nueva
            if (!string.IsNullOrWhiteSpace(usuarioUpdateDto.Password))
            {
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(usuarioUpdateDto.Password);
            }

            var updatedUsuario = await _usuarioRepository.UpdateAsync(usuario);
            return _mapper.Map<UsuarioDto>(updatedUsuario);
        }

        public async Task DeleteAsync(long id)
        {
            if (!await _usuarioRepository.ExistsAsync(id))
            {
                throw new KeyNotFoundException("Usuario no encontrado");
            }

            await _usuarioRepository.DeleteAsync(id);
        }

        public async Task<UsuarioDto?> LoginAsync(LoginDto loginDto)
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(loginDto.Email);
            
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, usuario.Password))
            {
                return null;
            }

            return _mapper.Map<UsuarioDto>(usuario);
        }

        public async Task UpdatePasswordAsync(long userId, string newPassword)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(userId);
            if (usuario == null)
            {
                throw new KeyNotFoundException("Usuario no encontrado");
            }

            usuario.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _usuarioRepository.UpdateAsync(usuario);
        }
    }
}
