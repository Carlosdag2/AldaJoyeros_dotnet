using AldaJoyeros.Entities;

namespace AldaJoyeros.Repositories.Interfaces
{
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token);
        Task<PasswordResetToken> CreateAsync(PasswordResetToken token);
        Task UpdateAsync(PasswordResetToken token);
        Task DeleteExpiredTokensAsync();
        Task InvalidateUserTokensAsync(long userId);
    }
}
