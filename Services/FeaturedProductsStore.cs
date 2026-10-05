using MySqlConnector;
using System.Text.Json;

namespace AldaJoyeros.Services;

public sealed class FeaturedProductsStore(IConfiguration configuration)
{
    public const int Limit = 8;
    private readonly SemaphoreSlim schemaGate = new(1, 1);
    private bool ready;
    private MySqlConnection Connection() => new(configuration.GetConnectionString("DefaultConnection"));

    private async Task EnsureAsync()
    {
        if (ready) return;
        await schemaGate.WaitAsync();
        try
        {
            if (ready) return;
            await using var connection = Connection();
            await connection.OpenAsync();
            await using var create = new MySqlCommand("CREATE TABLE IF NOT EXISTS productos_destacados_config (id int PRIMARY KEY, productos_json longtext NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", connection);
            await create.ExecuteNonQueryAsync();
            var initial = new List<long>();
            await using (var query = new MySqlCommand("SELECT id FROM producto WHERE eliminado=0 ORDER BY id LIMIT 8", connection))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) initial.Add(reader.GetInt64(0));
            await using var seed = new MySqlCommand("INSERT IGNORE INTO productos_destacados_config (id,productos_json) VALUES (1,@ids)", connection);
            seed.Parameters.AddWithValue("@ids", JsonSerializer.Serialize(initial));
            await seed.ExecuteNonQueryAsync();
            ready = true;
        }
        finally { schemaGate.Release(); }
    }

    public async Task<List<long>> GetIdsAsync()
    {
        await EnsureAsync();
        await using var connection = Connection();
        await connection.OpenAsync();
        await using var query = new MySqlCommand("SELECT productos_json FROM productos_destacados_config WHERE id=1", connection);
        return JsonSerializer.Deserialize<List<long>>((string)(await query.ExecuteScalarAsync())!)!;
    }

    public async Task ChangeAsync(long id, string action)
    {
        if (id <= 0 || action is not ("añadir" or "quitar" or "subir" or "bajar"))
            throw new ArgumentException("La acción solicitada no es válida.");
        await EnsureAsync();
        await using var connection = Connection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var query = new MySqlCommand("SELECT productos_json FROM productos_destacados_config WHERE id=1 FOR UPDATE", connection, transaction);
        var ids = JsonSerializer.Deserialize<List<long>>((string)(await query.ExecuteScalarAsync())!)!;
        var index = ids.IndexOf(id);
        if (action == "añadir" && index < 0)
        {
            if (ids.Count >= Limit) throw new ArgumentException("Puedes destacar como máximo ocho productos. Quita uno antes de añadir otro.");
            await using var active = new MySqlCommand("SELECT COUNT(*) FROM producto WHERE id=@id AND eliminado=0", connection, transaction);
            active.Parameters.AddWithValue("@id", id);
            if (Convert.ToInt32(await active.ExecuteScalarAsync()) != 1) throw new ArgumentException("Solo puedes destacar productos visibles del catálogo.");
            ids.Add(id);
        }
        else if (action == "quitar") ids.Remove(id);
        else if (action is "subir" or "bajar" && index >= 0)
        {
            var target = index + (action == "subir" ? -1 : 1);
            if (target >= 0 && target < ids.Count) (ids[index], ids[target]) = (ids[target], ids[index]);
        }
        await using var save = new MySqlCommand("UPDATE productos_destacados_config SET productos_json=@ids WHERE id=1", connection, transaction);
        save.Parameters.AddWithValue("@ids", JsonSerializer.Serialize(ids));
        await save.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
