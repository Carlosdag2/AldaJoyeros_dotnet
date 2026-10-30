using MySqlConnector;
namespace DuwardSync;
public static class Schema {
    public static async Task Ensure(MySqlConnection sql) {
        const string metadata = """
            CREATE TABLE IF NOT EXISTS producto_proveedor (
              proveedor varchar(32) NOT NULL,
              referencia_externa varchar(64) NOT NULL,
              referencia varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
              producto_id bigint NOT NULL,
              marca varchar(100) NOT NULL,
              seccion varchar(100) NOT NULL,
              coleccion varchar(200) NOT NULL,
              descripcion_completa longtext NOT NULL,
              pvp decimal(12,2) NOT NULL,
              moneda varchar(3) NOT NULL,
              disponibilidad_proveedor varchar(100) NOT NULL,
              cantidad_proveedor int NULL,
              caracteristicas_json json NOT NULL,
              datos_json json NOT NULL,
              imagenes_json json NOT NULL,
              url_origen varchar(1000) NOT NULL,
              estado varchar(30) NOT NULL,
              creado_por_importacion tinyint(1) NOT NULL,
              fecha_captura datetime(6) NOT NULL,
              fecha_importacion datetime(6) NOT NULL,
              PRIMARY KEY(proveedor,referencia_externa),
              UNIQUE KEY uq_proveedor_referencia(proveedor,referencia),
              KEY idx_producto_proveedor_producto(producto_id),
              CONSTRAINT fk_producto_proveedor_producto FOREIGN KEY(producto_id) REFERENCES producto(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """;
        await using(var command=new MySqlCommand(metadata,sql))await command.ExecuteNonQueryAsync();
        // An empty installation may have the smaller EF metadata table. Add only missing columns.
        var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using(var inspect=new MySqlCommand("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='producto_proveedor'",sql)) {
            await using var reader=await inspect.ExecuteReaderAsync();while(await reader.ReadAsync())columns.Add(reader.GetString(0));
        }
        var missingReference=!columns.Contains("referencia");
        foreach(var pair in new Dictionary<string,string>{
            ["referencia"]="varchar(100) NOT NULL DEFAULT ''",["seccion"]="varchar(100) NOT NULL DEFAULT ''",
            ["pvp"]="decimal(12,2) NOT NULL DEFAULT 0",["moneda"]="varchar(3) NOT NULL DEFAULT 'EUR'",
            ["disponibilidad_proveedor"]="varchar(100) NOT NULL DEFAULT ''",["cantidad_proveedor"]="int NULL",
            ["datos_json"]="json NULL",["imagenes_json"]="json NULL",["url_origen"]="varchar(1000) NOT NULL DEFAULT ''",
            ["fecha_captura"]="datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)",["fecha_importacion"]="datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)"
        })if(!columns.Contains(pair.Key)) {
            await using var add=new MySqlCommand("ALTER TABLE producto_proveedor ADD COLUMN `"+pair.Key+"` "+pair.Value,sql);await add.ExecuteNonQueryAsync();
        }
        if(missingReference){await using var backfill=new MySqlCommand("UPDATE producto_proveedor s JOIN producto p ON p.id=s.producto_id SET s.referencia=p.nombre WHERE s.referencia=''",sql);await backfill.ExecuteNonQueryAsync();}
        const string state="""
        CREATE TABLE IF NOT EXISTS duward_product_sync (
            referencia varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci PRIMARY KEY,
            producto_id bigint NOT NULL,
            estado_json json NOT NULL,
            actualizado_utc datetime(6) NOT NULL,
            CONSTRAINT fk_duward_sync_producto FOREIGN KEY(producto_id) REFERENCES producto(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;
        await using var create=new MySqlCommand(state,sql);await create.ExecuteNonQueryAsync();
    }
}
