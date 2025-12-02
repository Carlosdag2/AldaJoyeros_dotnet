using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(UsuarioDto usuario);
        long? ValidateToken(string token);
        string? GetUserRoleFromToken(string token);
        UsuarioDto? GetUserFromToken(string token);
    }
}
