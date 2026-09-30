using MySqlConnector;
namespace PerezMoraSync;
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
        const string state="""
        CREATE TABLE IF NOT EXISTS perez_mora_product_sync (
            referencia varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci PRIMARY KEY,
            producto_id bigint NOT NULL,
            estado_json json NOT NULL,
            actualizado_utc datetime(6) NOT NULL,
            CONSTRAINT fk_perez_mora_sync_producto FOREIGN KEY(producto_id) REFERENCES producto(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;
        await using var create=new MySqlCommand(state,sql);await create.ExecuteNonQueryAsync();
    }
}
