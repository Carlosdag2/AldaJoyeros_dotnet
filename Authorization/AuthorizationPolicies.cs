using Microsoft.AspNetCore.Authorization;

namespace AldaJoyeros.Authorization
{
    /// <summary>
    /// Configura las políticas de autorización de la aplicación
    /// </summary>
    public static class AuthorizationPolicies
    {
        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            // Política para administradores
            options.AddPolicy(Policies.RequireAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Admin);
            });

            // Política para usuarios autenticados (cualquier rol)
            options.AddPolicy(Policies.RequireAuthenticated, policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            // Política para rol USER específico
            options.AddPolicy(Policies.RequireUser, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.User, Roles.Admin); // Admin también puede actuar como user
            });
        }
    }
}
