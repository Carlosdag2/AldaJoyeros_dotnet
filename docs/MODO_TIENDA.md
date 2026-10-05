# Escaparate y tienda online

En el panel de administración, «Modo de la web» permite elegir:

- **Escaparate** (modo inicial): catálogo, precios, imágenes y características visibles, con contacto telefónico en la ficha del producto. No se muestran el carrito, los botones de compra ni los mensajes de envío. El servidor bloquea las operaciones de carrito, checkout, creación de pagos y finalización de pedidos, incluso si se solicitan directamente o desde una pestaña abierta anteriormente.
- **Tienda online**: se recuperan las funciones existentes de carrito y compra. Se sigue necesitando la configuración habitual de pagos, correo y conexiones de la aplicación.

El cambio afecta a las siguientes peticiones de toda la web y queda guardado tras reiniciar. Solo un usuario ADMIN puede modificarlo y el formulario requiere protección antifalsificación. No se eliminan productos, carritos, pedidos, imágenes ni pagos previos. El administrador conserva la gestión de pedidos; los clientes pueden consultar pedidos anteriores mediante sus rutas existentes.

## Base de datos y despliegue

La aplicación crea automáticamente en la base MySQL de `DefaultConnection` una tabla `tienda_config` con una fila (`id=1`, `ecommerce=0`). MongoDB no necesita cambios. La cuenta de MySQL debe poder crear esa tabla en la primera ejecución. Si en producción no tiene ese permiso, ejecutar previamente con una cuenta de migraciones:

```sql
CREATE TABLE IF NOT EXISTS tienda_config (
    id INT PRIMARY KEY,
    ecommerce TINYINT(1) NOT NULL DEFAULT 0
);
INSERT IGNORE INTO tienda_config (id, ecommerce) VALUES (1, 0);
```

Las instalaciones nuevas comienzan en escaparate. Migrar también esta tabla para conservar el modo al cambiar de alojamiento. Todas las instancias de la aplicación deben compartir la misma base MySQL. Las sincronizaciones de proveedores continúan funcionando independientemente del modo.

## Verificación

`Tests/PerezMoraChecks` incluye la opción `--store-mode`: comprueba el modo inicial y la persistencia en una base temporal, y prueba permisos, protección antifalsificación, bloqueos de compra, botones, filtros AJAX y adaptación móvil frente a una instancia local en el puerto 5098. Restaura el modo original al terminar. No realiza compras, pagos ni modifica el catálogo.
