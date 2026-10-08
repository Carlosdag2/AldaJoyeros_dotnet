using AldaJoyeros.Catalog;
using MySqlConnector;
using System.Text.Json;

static class SpecificCategories
{
    public static async Task Run(MySqlConnection sql, MySqlTransaction tx, string root, bool apply)
    {
        async Task<List<Dictionary<string, object?>>> Rows(string query)
        {
            await using var cmd = new MySqlCommand(query, sql, tx);
            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }
        async Task Execute(string query, params (string Key, object Value)[] parameters)
        {
            await using var cmd = new MySqlCommand(query, sql, tx);
            foreach (var (key, value) in parameters) cmd.Parameters.AddWithValue(key, value);
            await cmd.ExecuteNonQueryAsync();
        }
        var categories = await Rows("SELECT * FROM categoria ORDER BY id FOR UPDATE");
        var products = await Rows("SELECT * FROM producto ORDER BY id FOR UPDATE");
        var states = await Rows("SELECT * FROM perez_mora_product_sync ORDER BY referencia FOR UPDATE");
        var providers = await Rows("SELECT producto_id,proveedor,datos_json,caracteristicas_json,estado FROM producto_proveedor ORDER BY producto_id,proveedor");
        long Id(Dictionary<string, object?> row, string key = "id") => Convert.ToInt64(row[key]);
        var names = categories.ToDictionary(c => Id(c), c => (string)c["nombre"]!);
        var metadata = providers.GroupBy(p => Id(p, "producto_id")).ToDictionary(g => g.Key, g => g.FirstOrDefault(p => (string)p["estado"]! == "completo"));
        var previous = states.ToDictionary(s => Id(s, "producto_id"), s => JsonDocument.Parse((string)s["estado_json"]!).RootElement.Clone());
        var changes = new List<(long Id, long Old, string Name, bool Visible)>();
        var manual = 0;
        foreach (var product in products)
        {
            if (product["categoria_id"] == null || !names.TryGetValue(Id(product,"categoria_id"), out var category)) continue;
            if (previous.TryGetValue(Id(product), out var state) &&
                (state.GetProperty("manualCategory").GetBoolean() || state.GetProperty("lastCategory").GetInt64() != Id(product,"categoria_id"))) { manual++; continue; }
            var description = product["descripcion"]?.ToString() ?? "";
            string metal = "", subtype = "";
            if (metadata.TryGetValue(Id(product), out var provider) && provider != null)
            {
                using var data = JsonDocument.Parse((string)provider["datos_json"]!);
                if ((string)provider["proveedor"]! == "PerezMora")
                {
                    if (data.RootElement.TryGetProperty("METAL", out var m)) metal = m.ToString();
                    if (data.RootElement.TryGetProperty("TIPO", out var t)) subtype = t.ToString();
                }
                else if (data.RootElement.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Array)
                    foreach (var attribute in attributes.EnumerateArray())
                        if (attribute.TryGetProperty("name", out var key) && key.GetString() == "Material Principal" && attribute.TryGetProperty("value", out var value)) metal = value.ToString();
            }
            var target = CatalogTaxonomy.Specific(category, description, metal, subtype);
            if (target != category) changes.Add((Id(product), Id(product,"categoria_id"), target, !Convert.ToBoolean(product["eliminado"])));
        }
        var plan = changes.GroupBy(c => c.Name).OrderBy(g => g.Key).Select(g => new { name = g.Key, total = g.Count(), visible = g.Count(c => c.Visible) }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { changed = changes.Count, manualPreserved = manual, categories = plan }, new JsonSerializerOptions { WriteIndented = true }));
        if (!apply) { await tx.RollbackAsync(); return; }
        var output = Path.Combine(root, "App_Data", "CategoryOrganization", "specific-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "respaldo.json"), JsonSerializer.Serialize(new { categories, products, states }));
        foreach (var group in changes.GroupBy(c => c.Name))
        {
            await Execute("INSERT INTO categoria(nombre) VALUES(@name) ON DUPLICATE KEY UPDATE nombre=VALUES(nombre)", ("@name", group.Key));
            await using var find = new MySqlCommand("SELECT id FROM categoria WHERE nombre=@name", sql, tx);
            find.Parameters.AddWithValue("@name", group.Key);
            var newId = Convert.ToInt64(await find.ExecuteScalarAsync());
            foreach (var change in group)
            {
                await Execute("UPDATE producto SET categoria_id=@new WHERE id=@id AND categoria_id=@old", ("@new", newId), ("@id", change.Id), ("@old", change.Old));
                await Execute("UPDATE perez_mora_product_sync SET estado_json=JSON_SET(estado_json,'$.lastCategory',@new) WHERE producto_id=@id AND CAST(JSON_UNQUOTE(JSON_EXTRACT(estado_json,'$.lastCategory')) AS UNSIGNED)=@old", ("@new", newId), ("@id", change.Id), ("@old", change.Old));
            }
        }
        var actual = await Rows("SELECT * FROM producto ORDER BY id");
        string Preserved(List<Dictionary<string, object?>> rows) => JsonSerializer.Serialize(rows.Select(r => r.Where(p => p.Key != "categoria_id").ToDictionary()));
        if (Preserved(actual) != Preserved(products)) throw new InvalidOperationException("Cambió un dato distinto de la categoría. Se cancela.");
        var broken = await Rows("SELECT p.id FROM producto p LEFT JOIN categoria c ON c.id=p.categoria_id WHERE p.categoria_id IS NOT NULL AND c.id IS NULL");
        if (broken.Count > 0) throw new InvalidOperationException("Categorías inválidas. Se cancela.");
        await tx.CommitAsync();
        await File.WriteAllTextAsync(Path.Combine(output,"resultado.json"),JsonSerializer.Serialize(new { changed = changes.Count, productsPreserved = actual.Count, manualPreserved = manual, categories = plan }));
        Console.WriteLine($"Reorganizados {changes.Count} productos. Conservados {actual.Count} productos y {manual} categorías manuales. Respaldo: {output}");
    }
}
