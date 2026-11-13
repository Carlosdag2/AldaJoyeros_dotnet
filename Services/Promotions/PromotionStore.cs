using System.Text.Json;
using MySqlConnector;

namespace AldaJoyeros.Services.Promotions;
// The same MySQL database as the catalog; no local-only configuration files.
public sealed class PromotionStore(IConfiguration configuration)
{
    readonly SemaphoreSlim schema = new(1, 1), history = new(1, 1);
    bool ready;
    Dictionary<long, decimal>? lastPrices;
    public MySqlConnection Connection() => new(configuration.GetConnectionString("DefaultConnection"));
    public async Task EnsureAsync()
    {
        if (ready)
            return;
        await schema.WaitAsync();
        try
        {
            if (ready)
                return;
            await using var c = Connection();
            await c.OpenAsync();
            foreach (var sql in new[]
            {
                "CREATE TABLE IF NOT EXISTS promociones_config(id int PRIMARY KEY, revision bigint NOT NULL, config_json longtext NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                "CREATE TABLE IF NOT EXISTS promociones_precios(id bigint AUTO_INCREMENT PRIMARY KEY, producto_id bigint NOT NULL, precio decimal(18,2) NOT NULL, fecha_utc datetime(6) NOT NULL, INDEX producto_fecha(producto_id,fecha_utc,id)) ENGINE=InnoDB",
                "CREATE TABLE IF NOT EXISTS promociones_suscripciones(usuario_id bigint PRIMARY KEY, email varchar(255) NOT NULL, acepta bool NOT NULL DEFAULT 0, version varchar(64) NOT NULL, fecha_utc datetime(6) NOT NULL, origen varchar(80) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                "CREATE TABLE IF NOT EXISTS promociones_campanas(id varchar(32) PRIMARY KEY, asunto varchar(160) NOT NULL, base_url varchar(1000) NOT NULL, productos_json longtext NOT NULL, fecha_utc datetime(6) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                "CREATE TABLE IF NOT EXISTS promociones_envios(campana_id varchar(32) NOT NULL,usuario_id bigint NOT NULL,email varchar(255) NOT NULL,version varchar(64) NOT NULL,estado varchar(20) NOT NULL DEFAULT 'pendiente',actualizado_utc datetime(6) NOT NULL,PRIMARY KEY(campana_id,usuario_id),INDEX estado_fecha(estado,actualizado_utc)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
            }

            )
            {
                await using var cmd = new MySqlCommand(sql, c);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new MySqlCommand("INSERT IGNORE INTO promociones_config VALUES(1,0,@json)", c))
            {
                cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(new PromotionConfig()));
                await cmd.ExecuteNonQueryAsync();
            }

            ready = true;
        }
        finally
        {
            schema.Release();
        }
    }

    public async Task<PromotionConfig> GetAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT config_json FROM promociones_config WHERE id=1", c);
        return JsonSerializer.Deserialize<PromotionConfig>((string)(await q.ExecuteScalarAsync())!)!;
    }

    public async Task ChangeAsync(long revision, Action<PromotionConfig> edit)
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        PromotionConfig state;
        await using (var q = new MySqlCommand("SELECT config_json FROM promociones_config WHERE id=1 FOR UPDATE", c, tx))
            state = JsonSerializer.Deserialize<PromotionConfig>((string)(await q.ExecuteScalarAsync())!)!;
        if (state.Revision != revision)
            throw new InvalidOperationException("Otro administrador ha cambiado las promociones. Actualiza esta página antes de guardar.");
        edit(state);
        state.Revision++;
        await using var update = new MySqlCommand("UPDATE promociones_config SET revision=@r,config_json=@j WHERE id=1", c, tx);
        update.Parameters.AddWithValue("@r", state.Revision);
        update.Parameters.AddWithValue("@j", JsonSerializer.Serialize(state));
        await update.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    public async Task<Dictionary<long, decimal>> ReferencesAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT h.producto_id,MIN(h.precio) FROM promociones_precios h WHERE h.fecha_utc>=UTC_TIMESTAMP()-INTERVAL 30 DAY OR h.id=(SELECT MAX(p.id) FROM promociones_precios p WHERE p.producto_id=h.producto_id AND p.fecha_utc<UTC_TIMESTAMP()-INTERVAL 30 DAY) GROUP BY h.producto_id", c);
        var result = new Dictionary<long, decimal>();
        await using var rd = await q.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            result[rd.GetInt64(0)] = rd.GetDecimal(1);
        return result;
    }

    public async Task ObserveAsync(IEnumerable<(long Id, decimal Price)> prices)
    {
        await EnsureAsync();
        await history.WaitAsync();
        try
        {
            await using var c = Connection();
            await c.OpenAsync();
            if (lastPrices == null)
            {
                lastPrices = new();
                await using var q = new MySqlCommand("SELECT h.producto_id,h.precio FROM promociones_precios h INNER JOIN (SELECT producto_id,MAX(id) id FROM promociones_precios GROUP BY producto_id) latest ON latest.id=h.id", c);
                await using var rd = await q.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    lastPrices[rd.GetInt64(0)] = rd.GetDecimal(1);
            }

            var changes = prices.Where(p => !lastPrices.TryGetValue(p.Id, out var previous) || previous != p.Price).DistinctBy(p => p.Id).ToArray();
            foreach (var chunk in changes.Chunk(500))
            {
                await using var q = new MySqlCommand
                {
                    Connection = c
                };
                var vals = new List<string>();
                for (int i = 0; i < chunk.Length; i++)
                {
                    vals.Add($"(@i{i},@p{i},UTC_TIMESTAMP(6))");
                    q.Parameters.AddWithValue("@i" + i, chunk[i].Id);
                    q.Parameters.AddWithValue("@p" + i, chunk[i].Price);
                }

                q.CommandText = "INSERT INTO promociones_precios(producto_id,precio,fecha_utc) VALUES " + string.Join(',', vals);
                await q.ExecuteNonQueryAsync();
                foreach (var p in chunk)
                    lastPrices[p.Id] = p.Price;
            }
        }
        finally
        {
            history.Release();
        }
    }

    public async Task SubscribeAsync(long userId, bool accepts, string source)
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("INSERT INTO promociones_suscripciones(usuario_id,email,acepta,version,fecha_utc,origen) SELECT id,email,@yes,@v,UTC_TIMESTAMP(6),@source FROM usuario WHERE id=@id ON DUPLICATE KEY UPDATE email=VALUES(email),acepta=VALUES(acepta),version=VALUES(version),fecha_utc=VALUES(fecha_utc),origen=VALUES(origen)", c);
        q.Parameters.AddWithValue("@id", userId);
        q.Parameters.AddWithValue("@yes", accepts);
        q.Parameters.AddWithValue("@v", Guid.NewGuid().ToString("N"));
        q.Parameters.AddWithValue("@source", source);
        await q.ExecuteNonQueryAsync();
    }

    public async Task<bool> SubscribedAsync(long id)
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT acepta FROM promociones_suscripciones s JOIN usuario u ON u.id=s.usuario_id AND u.email=s.email WHERE usuario_id=@id", c);
        q.Parameters.AddWithValue("@id", id);
        return Convert.ToBoolean(await q.ExecuteScalarAsync() ?? false);
    }

    public async Task<int> AudienceAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT COUNT(DISTINCT LOWER(s.email)) FROM promociones_suscripciones s JOIN usuario u ON u.id=s.usuario_id AND u.email=s.email WHERE s.acepta=1", c);
        return Convert.ToInt32(await q.ExecuteScalarAsync());
    }

    public async Task<bool> UnsubscribeAsync(long id, string version)
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("UPDATE promociones_suscripciones SET acepta=0,fecha_utc=UTC_TIMESTAMP(6),origen='baja-correo' WHERE usuario_id=@id AND version=@v", c);
        q.Parameters.AddWithValue("@id", id);
        q.Parameters.AddWithValue("@v", version);
        return await q.ExecuteNonQueryAsync() > 0;
    }

    public async Task QueueAsync(string id, string subject, string baseUrl, List<PromotionEmailProduct> products)
    {
        await EnsureAsync();
        if (!Guid.TryParseExact(id, "N", out _) || subject.Length is < 3 or > 160 || products.Count == 0)
            throw new ArgumentException("Revisa el asunto y los productos del correo.");
        await using var c = Connection();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using (var q = new MySqlCommand("INSERT IGNORE INTO promociones_campanas VALUES(@id,@s,@b,@p,UTC_TIMESTAMP(6))", c, tx))
        {
            q.Parameters.AddWithValue("@id", id);
            q.Parameters.AddWithValue("@s", subject);
            q.Parameters.AddWithValue("@b", baseUrl);
            q.Parameters.AddWithValue("@p", JsonSerializer.Serialize(products));
            if (await q.ExecuteNonQueryAsync() == 0)
                return;
        }

        await using (var q = new MySqlCommand("INSERT INTO promociones_envios(campana_id,usuario_id,email,version,estado,actualizado_utc) SELECT @id,s.usuario_id,s.email,s.version,'pendiente',UTC_TIMESTAMP(6) FROM promociones_suscripciones s JOIN usuario u ON u.id=s.usuario_id AND u.email=s.email WHERE s.acepta=1 AND s.usuario_id=(SELECT MIN(s2.usuario_id) FROM promociones_suscripciones s2 JOIN usuario u2 ON u2.id=s2.usuario_id AND u2.email=s2.email WHERE s2.acepta=1 AND LOWER(s2.email)=LOWER(s.email))", c, tx))
        {
            q.Parameters.AddWithValue("@id", id);
            if (await q.ExecuteNonQueryAsync() == 0)
                throw new InvalidOperationException("Todavía no hay usuarios suscritos a las ofertas.");
        }

        await tx.CommitAsync();
    }

    public async Task<List<PromotionCampaign>> CampaignsAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT c.id,c.asunto,c.base_url,c.productos_json,c.fecha_utc,SUM(e.estado IN ('pendiente','enviando')),SUM(e.estado='enviado'),SUM(e.estado IN ('error','incierto')),SUM(e.estado='omitido') FROM promociones_campanas c JOIN promociones_envios e ON e.campana_id=c.id GROUP BY c.id ORDER BY c.fecha_utc DESC LIMIT 20", c);
        var result = new List<PromotionCampaign>();
        await using var rd = await q.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            result.Add(new(rd.GetString(0), rd.GetString(1), rd.GetString(2), JsonSerializer.Deserialize<List<PromotionEmailProduct>>(rd.GetString(3))!, rd.GetDateTime(4), Convert.ToInt32(rd.GetValue(5)), Convert.ToInt32(rd.GetValue(6)), Convert.ToInt32(rd.GetValue(7)), Convert.ToInt32(rd.GetValue(8))));
        return result;
    }

    public async Task<PromotionDelivery?> ClaimAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        PromotionDelivery? row = null;
        await using (var q = new MySqlCommand("SELECT e.campana_id,e.usuario_id,e.email,e.version,c.asunto,c.base_url,c.productos_json FROM promociones_envios e JOIN promociones_campanas c ON c.id=e.campana_id WHERE e.estado='pendiente' ORDER BY c.fecha_utc,e.usuario_id LIMIT 1 FOR UPDATE SKIP LOCKED", c, tx))
        await using (var rd = await q.ExecuteReaderAsync())
        {
            if (await rd.ReadAsync())
                row = new(rd.GetString(0), rd.GetInt64(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetString(5), JsonSerializer.Deserialize<List<PromotionEmailProduct>>(rd.GetString(6))!);
        }

        if (row != null)
        {
            await using var q = new MySqlCommand("UPDATE promociones_envios SET estado='enviando',actualizado_utc=UTC_TIMESTAMP(6) WHERE campana_id=@c AND usuario_id=@u", c, tx);
            q.Parameters.AddWithValue("@c", row.CampaignId);
            q.Parameters.AddWithValue("@u", row.UserId);
            await q.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return row;
    }

    public async Task<bool> EligibleAsync(PromotionDelivery d)
    {
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("SELECT COUNT(*) FROM promociones_suscripciones s JOIN usuario u ON u.id=s.usuario_id AND u.email=s.email WHERE s.usuario_id=@u AND s.email=@e AND s.version=@v AND s.acepta=1", c);
        q.Parameters.AddWithValue("@u", d.UserId);
        q.Parameters.AddWithValue("@e", d.Email);
        q.Parameters.AddWithValue("@v", d.Version);
        return Convert.ToInt32(await q.ExecuteScalarAsync()) == 1;
    }

    public async Task FinishAsync(PromotionDelivery d, string state)
    {
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("UPDATE promociones_envios SET estado=@s,actualizado_utc=UTC_TIMESTAMP(6) WHERE campana_id=@c AND usuario_id=@u", c);
        q.Parameters.AddWithValue("@s", state);
        q.Parameters.AddWithValue("@c", d.CampaignId);
        q.Parameters.AddWithValue("@u", d.UserId);
        await q.ExecuteNonQueryAsync();
    }

    public async Task RecoverAsync()
    {
        await EnsureAsync();
        await using var c = Connection();
        await c.OpenAsync();
        await using var q = new MySqlCommand("UPDATE promociones_envios SET estado='incierto' WHERE estado='enviando' AND actualizado_utc<UTC_TIMESTAMP()-INTERVAL 10 MINUTE", c);
        await q.ExecuteNonQueryAsync();
    }
}
