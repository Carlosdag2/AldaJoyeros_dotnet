using AldaJoyeros.Data;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AldaJoyeros.Repositories.Implementations
{
    public class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly AldaJoyerosContext _context;

        public PasswordResetTokenRepository(AldaJoyerosContext context)
        {
            _context = context;
        }

        public async Task<PasswordResetToken?> GetByTokenAsync(string token)
        {
            return await _context.PasswordResetTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<PasswordResetToken> CreateAsync(PasswordResetToken token)
        {
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();
            return token;
        }

        public async Task UpdateAsync(PasswordResetToken token)
        {
            _context.PasswordResetTokens.Update(token);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteExpiredTokensAsync()
        {
            var expiredTokens = await _context.PasswordResetTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow || t.Used)
                .ToListAsync();

            _context.PasswordResetTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();
        }

        public async Task InvalidateUserTokensAsync(long userId)
        {
            var userTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == userId && !t.Used)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.Used = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
