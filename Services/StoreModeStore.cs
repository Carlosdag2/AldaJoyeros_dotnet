using MySqlConnector;

namespace AldaJoyeros.Services;

public sealed class StoreModeStore(IConfiguration configuration)
{
    public const string ContextKey = "EcommerceEnabled";
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool ready;
    private MySqlConnection Connection() => new(configuration.GetConnectionString("DefaultConnection"));
    private async Task EnsureAsync()
    {
        if (ready) return;
        await gate.WaitAsync();
        try
        {
            if (ready) return;
            await using var connection = Connection(); await connection.OpenAsync();
            await using var command = new MySqlCommand("CREATE TABLE IF NOT EXISTS tienda_config (id int PRIMARY KEY, ecommerce tinyint(1) NOT NULL DEFAULT 0); INSERT IGNORE INTO tienda_config VALUES (1,0);", connection);
            await command.ExecuteNonQueryAsync(); ready = true;
        }
        finally { gate.Release(); }
    }
    public async Task<bool> EnabledAsync()
    {
        await EnsureAsync(); await using var connection = Connection(); await connection.OpenAsync();
        await using var command = new MySqlCommand("SELECT ecommerce FROM tienda_config WHERE id=1", connection);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }
    public async Task SetAsync(bool enabled)
    {
        await EnsureAsync(); await using var connection = Connection(); await connection.OpenAsync();
        await using var command = new MySqlCommand("UPDATE tienda_config SET ecommerce=@enabled WHERE id=1", connection);
        command.Parameters.AddWithValue("@enabled", enabled); await command.ExecuteNonQueryAsync();
    }
}
