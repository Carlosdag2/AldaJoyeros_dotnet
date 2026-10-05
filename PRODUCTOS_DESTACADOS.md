# Productos destacados

El administrador accede desde Panel de administración → Productos destacados. Puede buscar productos visibles por nombre, categoría o ID, añadir o quitar hasta ocho destacados y ordenar su aparición mediante las flechas. Cada cambio se guarda inmediatamente.

La selección se guarda en MySQL, en productos_destacados_config. La aplicación crea la tabla automáticamente al usar esta función por primera vez; el usuario de conexión necesita permiso para crearla en esa primera ejecución. Incluye esta tabla al migrar la base de datos. Las imágenes siguen cargándose desde MongoDB.

La selección inicial conserva los ocho primeros productos visibles. Después, una selección vacía permanece vacía. Los productos ocultos o eliminados no aparecen en el inicio, aunque sigan seleccionados; el panel avisa y permite quitarlos. Quitar un destacado no elimina el producto del catálogo.

Los cambios requieren un administrador y un token antifalsificación. El servidor valida la existencia y visibilidad de los productos, evita duplicados y limita la selección a ocho. La actualización utiliza una transacción y un bloqueo para evitar pérdidas de cambios simultáneos.

Pruebas: Tests/PerezMoraChecks con el argumento --featured. Las comprobaciones de persistencia y concurrencia crean y eliminan una base temporal aislada. Las pruebas de interfaz usan una instancia local en el puerto 5096 y no cambian la selección real.
