using AldaJoyeros.Catalog;
using MySqlConnector;
using System.Text.Json;

namespace AldaJoyeros.Services.PerezMora;

public sealed partial class PerezMoraStore
{
    public async Task<decimal> Coefficient(CancellationToken ct=default)
    {
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        await using var command=new MySqlCommand("SELECT coeficiente FROM perez_mora_pricing WHERE id=1",sql);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
    }

    public async Task<ProviderPriceBands> Bands(CancellationToken ct=default)
    {
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        await using var command=new MySqlCommand("SELECT coeficiente_a,coeficiente_b,coeficiente_c FROM perez_mora_pricing_bands WHERE id=1",sql);
        await using var reader=await command.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);
        return new(reader.GetDecimal(0),reader.GetDecimal(1),reader.GetDecimal(2));
    }

    public Task<PerezMoraPricePreview> Prices(decimal coefficient,bool apply,string backupRoot,CancellationToken ct=default)
        =>Prices(new ProviderPriceBands(coefficient,coefficient,coefficient),apply,backupRoot,ct);

    public async Task<PerezMoraPricePreview> Prices(ProviderPriceBands bands,bool apply,string backupRoot,CancellationToken ct=default)
    {
        bands.Validate();await Ensure(ct);
        await using var sql=Connection(false);await sql.OpenAsync(ct);
        var key="alda_perez_mora_import_"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql.Database))).ToLowerInvariant()[..16];
        await using(var mutex=new MySqlCommand("SELECT GET_LOCK(@key,0)",sql))
        {mutex.Parameters.AddWithValue("@key",key);if(Convert.ToInt32(await mutex.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Hay una actualización de Pérez Mora en marcha. Espera a que termine.");}
        await using var tx=await sql.BeginTransactionAsync(ct);
        var rows=new List<PerezMoraPriceRow>();
        // Only mapped products created by this supplier can receive automatic prices. Other suppliers never enter this query.
        await using(var command=new MySqlCommand("""
            SELECT p.id,pp.referencia,pp.pvp,p.precio,s.estado_json
            FROM producto p JOIN producto_proveedor pp ON pp.producto_id=p.id AND pp.proveedor='PerezMora' AND pp.creado_por_importacion=1
            JOIN perez_mora_product_sync s ON s.producto_id=p.id AND s.referencia=pp.referencia ORDER BY p.id FOR UPDATE
            """,sql,tx))
        {
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
            {
                var price=Convert.ToDecimal(reader.GetValue(3));var cost=reader.GetDecimal(2);var state=reader.GetString(4);
                using var data=JsonDocument.Parse(state);
                var manual=data.RootElement.GetProperty("manualPrice").GetBoolean() || !data.RootElement.GetProperty("created").GetBoolean() || Math.Abs(data.RootElement.GetProperty("lastPrice").GetDecimal()-price)>0.000001m;
                rows.Add(new(reader.GetInt64(0),reader.GetString(1),cost,price,manual?price:bands.Pvp(cost),manual,state));
            }
        }
        var preview=new PerezMoraPricePreview(bands.A,rows.Count(x=>!x.Manual&&x.NewPrice!=x.CurrentPrice),rows.Count(x=>x.Manual),rows.Take(8).Select(x=>x with{State=""}).ToArray(),bands);
        if(!apply){await tx.RollbackAsync(ct);return preview;}
        var run=Path.Combine(backupRoot,"pricing",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);
        ProviderPriceBands previous;
        await using(var command=new MySqlCommand("SELECT coeficiente_a,coeficiente_b,coeficiente_c FROM perez_mora_pricing_bands WHERE id=1 FOR UPDATE",sql,tx)) {
            await using var reader=await command.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);
            previous=new(reader.GetDecimal(0),reader.GetDecimal(1),reader.GetDecimal(2));
        }
        await File.WriteAllTextAsync(Path.Combine(run,"respaldo.json"),JsonSerializer.Serialize(new{createdUtc=DateTime.UtcNow,previousBands=previous,newBands=bands,products=rows}),ct);
        await using(var command=new MySqlCommand("""
            UPDATE producto p JOIN producto_proveedor pp ON pp.producto_id=p.id AND pp.proveedor='PerezMora' AND pp.creado_por_importacion=1
            JOIN perez_mora_product_sync s ON s.producto_id=p.id AND s.referencia=pp.referencia
            SET p.precio=ROUND(pp.pvp*(CASE WHEN pp.pvp<=100 THEN @a WHEN pp.pvp<=500 THEN @b ELSE @c END),2),s.estado_json=JSON_SET(s.estado_json,'$.lastPrice',ROUND(pp.pvp*(CASE WHEN pp.pvp<=100 THEN @a WHEN pp.pvp<=500 THEN @b ELSE @c END),2)),s.actualizado_utc=UTC_TIMESTAMP(6)
            WHERE JSON_UNQUOTE(JSON_EXTRACT(s.estado_json,'$.manualPrice'))='false' AND JSON_UNQUOTE(JSON_EXTRACT(s.estado_json,'$.created'))='true'
            AND ABS(p.precio-CAST(JSON_UNQUOTE(JSON_EXTRACT(s.estado_json,'$.lastPrice')) AS DECIMAL(20,6)))<=0.000001
            """,sql,tx))
        {command.Parameters.AddWithValue("@a",bands.A);command.Parameters.AddWithValue("@b",bands.B);command.Parameters.AddWithValue("@c",bands.C);await command.ExecuteNonQueryAsync(ct);}
        var expected=rows.ToDictionary(x=>x.Id);var verified=0;
        await using(var command=new MySqlCommand("""
            SELECT p.id,p.precio,s.estado_json FROM producto p
            JOIN producto_proveedor pp ON pp.producto_id=p.id AND pp.proveedor='PerezMora' AND pp.creado_por_importacion=1
            JOIN perez_mora_product_sync s ON s.producto_id=p.id AND s.referencia=pp.referencia
            """,sql,tx))
        {
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
            {
                if(!expected.TryGetValue(reader.GetInt64(0),out var row) || Math.Abs(Convert.ToDecimal(reader.GetValue(1))-row.NewPrice)>0.000001m)
                    throw new InvalidDataException("No se pudo verificar el recálculo de precios. No se ha aplicado ningún cambio.");
                using var state=JsonDocument.Parse(reader.GetString(2));
                if(!row.Manual && state.RootElement.GetProperty("lastPrice").GetDecimal()!=row.NewPrice)
                    throw new InvalidDataException("No se pudo verificar el precio de referencia. No se ha aplicado ningún cambio.");
                verified++;
            }
        }
        if(verified!=rows.Count)throw new InvalidDataException("Ha cambiado el catálogo durante el recálculo. No se ha aplicado ningún cambio.");
        await using(var command=new MySqlCommand("UPDATE perez_mora_pricing SET coeficiente=@factor,actualizado_utc=UTC_TIMESTAMP(6) WHERE id=1",sql,tx))
        {command.Parameters.AddWithValue("@factor",bands.A);await command.ExecuteNonQueryAsync(ct);}
        await using(var command=new MySqlCommand("UPDATE perez_mora_pricing_bands SET coeficiente_a=@a,coeficiente_b=@b,coeficiente_c=@c,actualizado_utc=UTC_TIMESTAMP(6) WHERE id=1",sql,tx))
        {command.Parameters.AddWithValue("@a",bands.A);command.Parameters.AddWithValue("@b",bands.B);command.Parameters.AddWithValue("@c",bands.C);await command.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(run,"resultado.json"),JsonSerializer.Serialize(preview),CancellationToken.None);
        return preview;
    }
}
