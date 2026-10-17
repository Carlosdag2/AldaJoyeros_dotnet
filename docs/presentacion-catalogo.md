# Presentación del catálogo en tienda

En Administración > Presentación del catálogo, abrir la presentación y pulsar Comenzar. Muestra una pieza cada cinco segundos, con imagen, descripción breve, referencia, categoría, precio y disponibilidad en 72 horas. La pantalla de reproducción no incluye navegación de administración, carrito ni datos de proveedor.

Cada vuelta mezcla todos los productos visibles con imagen mediante Fisher-Yates. No repite referencias dentro de la vuelta ni el último producto de una vuelta inmediatamente al iniciar la siguiente, salvo que solo haya un producto. Las piezas ocultas y sin imagen se excluyen. El listado se toma al iniciar; para incorporar nuevas referencias, salir y volver a comenzar. Si una pieza se oculta durante la reproducción, la descarga de su imagen se rechaza y se pasa a otra.

Pausar/Continuar, Siguiente y Pantalla completa están disponibles en la barra inferior, que se oculta durante la reproducción y reaparece al tocar, mover el ratón o usar el teclado. Espacio pausa y flecha derecha avanza. Pantalla completa requiere un navegador compatible; se solicita mantener encendida la pantalla cuando el dispositivo lo permite. Si no lo permite, configurar el tiempo de suspensión del dispositivo.

Para abrir la presentación y obtener su listado inicial se requiere sesión de administrador. Las imágenes se sirven desde un endpoint de solo lectura del catálogo visible, de modo que la sesión pueda caducar sin detener una presentación que ya está en marcha. No renueva ni alarga la sesión del administrador. Si la sesión ya ha caducado al comenzar o se interrumpe la conexión, se muestra un mensaje para recuperarla. No inicia servidores ni tareas fuera de la aplicación.

El listado inicial contiene metadatos, no imágenes en Base64. Solo se conserva la foto actual y una precargada; se liberan las anteriores para limitar la memoria durante ejecuciones largas. Las consultas y la reproducción son de lectura y no modifican las bases de datos. Todos los endpoints de AdminPresentacion conservan la protección AdminOnly. CatalogoPantalla/Imagen comprueba que el producto continúa visible antes de devolver su foto; no sirve imágenes de productos ocultos ni huérfanas.

Comprobación del orden: `node Tests/CatalogPresentationChecks/deck.test.cjs` cubre catálogos vacíos, un único producto, duplicados, cambios de vuelta y hasta 28.000 piezas.
