using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IUsuarioService
    {
        Task<IEnumerable<UsuarioDto>> GetAllAsync();
        Task<UsuarioDto?> GetByIdAsync(long id);
        Task<UsuarioDto?> GetByEmailAsync(string email);
        Task<UsuarioDto> CreateAsync(UsuarioCreateDto usuarioCreateDto);
        Task<UsuarioDto> UpdateAsync(long id, UsuarioUpdateDto usuarioUpdateDto);
        Task DeleteAsync(long id);
        Task<UsuarioDto?> LoginAsync(LoginDto loginDto);
        Task UpdatePasswordAsync(long userId, string newPassword);
    }
}
