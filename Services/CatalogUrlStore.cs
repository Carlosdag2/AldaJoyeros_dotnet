using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace AldaJoyeros.Services;

// Stable, unique public names, independent of database IDs and category/name edits.
public sealed class CatalogUrlStore(IConfiguration configuration)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile IReadOnlyDictionary<string,string> known = new Dictionary<string,string>();
    public static Dictionary<string,string> Allocate(IEnumerable<KeyValuePair<string,string>> requests, IReadOnlyDictionary<string,string> existing)
    {
        var result = new Dictionary<string,string>(existing, StringComparer.Ordinal);
        var taken = result.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (result.ContainsKey(request.Key)) continue;
            var stem = request.Value;
            var candidate = stem; var ordinal = 1;
            while (!taken.Add(candidate)) candidate = stem + "-alternativa-" + (++ordinal);
            result.Add(request.Key, candidate);
        }
        return result;
    }
    private static string Key(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task<IReadOnlyDictionary<string,string>> GetAsync(IEnumerable<KeyValuePair<string,string>> requests)
    {
        var wanted = requests.Select(x => KeyValuePair.Create(Key(x.Key), x.Value)).DistinctBy(x => x.Key).ToArray();
        var snapshot = known;
        if (wanted.All(x => snapshot.ContainsKey(x.Key))) return wanted.ToDictionary(x => x.Key, x => snapshot[x.Key]);
        await gate.WaitAsync();
        try
        {
            snapshot = known;
            if (wanted.All(x => snapshot.ContainsKey(x.Key))) return wanted.ToDictionary(x => x.Key, x => snapshot[x.Key]);
            await using var connection = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();
            await using (var create = new MySqlCommand("CREATE TABLE IF NOT EXISTS catalogo_urls (clave char(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY, slug varchar(180) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE) ENGINE=InnoDB", connection))
                await create.ExecuteNonQueryAsync();
            await using var acquire = new MySqlCommand("SELECT GET_LOCK(SHA2(CONCAT('alda_catalog_urls:', DATABASE()),256),15)", connection);
            if (Convert.ToInt32(await acquire.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("No se pudo reservar la dirección del catálogo. Vuelve a intentarlo.");
            try
            {
                var existing = new Dictionary<string,string>();
                await using (var read = new MySqlCommand("SELECT clave,slug FROM catalogo_urls", connection))
                await using (var reader = await read.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) existing.Add(reader.GetString(0), reader.GetString(1));
                var assigned = Allocate(wanted, existing);
                foreach (var batch in assigned.Where(x => !existing.ContainsKey(x.Key)).Chunk(200))
                {
                    await using var insert = new MySqlCommand { Connection = connection };
                    var values = new List<string>();
                    for (var i = 0; i < batch.Length; i++)
                    {
                        values.Add($"(@key{i},@slug{i})");
                        insert.Parameters.AddWithValue("@key" + i, batch[i].Key);
                        insert.Parameters.AddWithValue("@slug" + i, batch[i].Value);
                    }
                    insert.CommandText = "INSERT INTO catalogo_urls (clave,slug) VALUES " + string.Join(',', values);
                    await insert.ExecuteNonQueryAsync();
                }
                known = assigned;
                return wanted.ToDictionary(x => x.Key, x => assigned[x.Key]);
            }
            finally
            {
                await using var release = new MySqlCommand("SELECT RELEASE_LOCK(SHA2(CONCAT('alda_catalog_urls:', DATABASE()),256))", connection);
                await release.ExecuteScalarAsync();
            }
        }
        finally { gate.Release(); }
    }
    public static string Lookup(IReadOnlyDictionary<string,string> assigned, string key) => assigned[Key(key)];
}
