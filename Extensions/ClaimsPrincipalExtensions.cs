using System.Security.Claims;

namespace AldaJoyeros.Extensions
{
    /// <summary>
    /// Extensiones para acceder a la información del usuario autenticado de forma limpia y tipada
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        public static long? GetUserId(this ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public static string? GetEmail(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static string GetRole(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Role)?.Value ?? "USER";
        }

        public static bool IsAdmin(this ClaimsPrincipal principal)
        {
            return principal.GetRole() == "ADMIN";
        }

        public static bool IsAuthenticated(this ClaimsPrincipal principal)
        {
            return principal.Identity?.IsAuthenticated == true && principal.GetUserId().HasValue;
        }
    }
}
