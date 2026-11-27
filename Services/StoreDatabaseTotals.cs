using AldaJoyeros.Data;
using Microsoft.EntityFrameworkCore;
namespace AldaJoyeros.Services;

// Unmapped read-only result; no model, index or database migration is required.
public sealed class StoreDatabaseTotals
{
    public int Productos { get; set; }
    public int Categorias { get; set; }
    public int Usuarios { get; set; }
    public static Task<StoreDatabaseTotals> ReadAsync(AldaJoyerosContext sql, CancellationToken ct = default) =>
        sql.Database.SqlQueryRaw<StoreDatabaseTotals>("SELECT (SELECT COUNT(*) FROM producto WHERE eliminado = 0) AS Productos, (SELECT COUNT(*) FROM categoria) AS Categorias, (SELECT COUNT(*) FROM usuario) AS Usuarios").SingleAsync(ct);
}
