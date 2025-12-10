namespace AldaJoyeros.Authorization
{
    /// <summary>
    /// Nombres de políticas de autorización centralizados
    /// </summary>
    public static class Policies
    {
        public const string RequireAdmin = nameof(RequireAdmin);
        public const string RequireUser = nameof(RequireUser);
        public const string RequireAuthenticated = nameof(RequireAuthenticated);
    }

    /// <summary>
    /// Roles disponibles en la aplicación
    /// </summary>
    public static class Roles
    {
        public const string Admin = "ADMIN";
        public const string User = "USER";
    }
}
