# Optimización reversible de consultas

Esta fase mantiene MySQL, MongoDB y los originales de las imágenes en su ubicación actual. No migra datos, añade índices, elimina imágenes ni convierte originales. Mejora las consultas sin cambiar precios, búsqueda, tallas, disponibilidad o direcciones públicas.

## Cambios

- Las imágenes del catálogo se consultan por lotes de hasta 200 identificadores, leyendo solamente metadatos. Los archivos binarios no se descargan para construir las tarjetas.
- Los metadatos y las ausencias se reutilizan únicamente dentro de la misma petición. Una petición nueva vuelve a consultar MongoDB, para reflejar las imágenes añadidas o eliminadas.
- En familias con tallas se conserva la prioridad anterior: representante primero y, si no tiene fotos, primera variante activa con imágenes. No se cargan de antemano todas las fotos de todas las tallas.
- Se reutiliza el índice de familias dentro de la petición.
- Si los precios observados no han cambiado y el historial ya está inicializado en memoria, no se abre otra conexión MySQL para comprobar que no hace falta insertar nada. Se conserva el bloqueo existente y los cambios reales siguen guardándose.

## Revisión ampliada de consultas

Además de las imágenes públicas, el mismo interruptor controla estas mejoras:

- La versión de MySQL se detecta una vez por proceso. Si el servidor falla durante la detección, el resultado fallido no se guarda y se puede reintentar después. No se fuerza una versión incompatible.
- Las categorías se leen como identificador, nombre y recuento, sin cargar todos sus productos. Se reutilizan durante la petición y se invalidan al iniciar una operación de edición de categorías. Las vistas reciben copias para no alterar los recuentos de otros consumidores.
- El filtrado de categorías del catálogo reutiliza esa misma lectura.
- Los listados de usuarios reciben solamente los campos del listado y el número de pedidos; no necesitan descargar contraseñas ni pedidos completos.
- Las fotos de administración, carrito y pedidos se leen por lotes. Se mantienen sus bytes y las fotos de productos eliminados que deban mostrarse en pedidos históricos.
- El contador de la cabecera suma cantidades en SQL; no descarga los productos. Las respuestas JSON, redirecciones y fragmentos no solicitan un contador que no van a mostrar.
- El carrito invitado consulta los productos por lote y reutiliza la instantánea de catálogo de la petición para precios y disponibilidad. Padres y tallas mantienen sus restricciones.
- Los tres totales generales del panel se obtienen con una consulta de solo lectura. Las métricas agrupan también los recuentos y ventas por períodos: siete consultas menos para los mismos datos.
- Las fotos no consultan el modo de tienda, porque se sirven tanto en escaparate como en ecommerce. Conservan la comprobación de visibilidad del producto, el ETag y la caché HTTP.

Seguir viendo varios `SELECT` es normal: una pantalla consulta datos distintos y cada petición de foto comprueba su producto. Los trabajadores de proveedores conservan las comprobaciones de conexión y liderazgo que necesitan para ejecutar las actualizaciones con seguridad. No se han ocultado mensajes SQL ni desactivado funciones para aparentar una reducción.

## Volver al funcionamiento anterior

1. Abre `appsettings.Optimization.json` en la raíz del proyecto.
2. Cambia `"Enabled": true` por `"Enabled": false` y guarda.
3. Detén y vuelve a iniciar la aplicación desde Visual Studio.

Con `false`, las imágenes se consultan individualmente sin la reutilización de metadatos, y el historial de precios vuelve a abrir la conexión como antes. También se recuperan la detección de versión por contexto, las lecturas anteriores de categorías y usuarios, las agregaciones separadas y la comprobación del modo en las fotos. No hace falta restaurar las bases de datos: no se ha realizado ninguna migración.

El archivo contiene solamente esta configuración:

```json
{
  "StorageOptimization": {
    "Enabled": false
  }
}
```

`appsettings.Local.json`, las variables de entorno y los argumentos de inicio tienen prioridad. Si alguno define `StorageOptimization:Enabled`, hay que cambiar esa definición. La variable de entorno equivalente es `StorageOptimization__Enabled=false`. Los cambios requieren reiniciar la aplicación.

## Respaldo del código

Antes de los cambios se creó la etiqueta local `respaldo/antes-optimizacion-consultas-20261127`, que apunta a `b6b6aa2b7261f1e2e7161f9a05cd5ad96e065d94`, y una copia ZIP del código en la carpeta de trabajo de la tarea. Esa copia es del código, no un volcado de las bases de datos; excluye salidas de compilación y la configuración principal local. La configuración privada y las bases existentes permanecen en su sitio.

## Validación

`Tests/StorageOptimizationChecks` compara ambos modos en bases MySQL y MongoDB creadas con nombres aleatorios exclusivos de la prueba. No utiliza la base configurada como destino: reutiliza la conexión para crear bases temporales independientes. Requiere permisos locales para crear esas bases y activar el perfilador únicamente en la base MongoDB temporal. Ambas se eliminan al terminar.

```powershell
dotnet run --project Tests/StorageOptimizationChecks -- RUTA_DEL_PROYECTO
```

Las comparaciones incluyen métricas vacías y con pedidos cancelados, categorías, usuarios, contador e importe del carrito, consulta conjunta de productos y disponibilidad de padres y tallas.

En las pruebas se verificaron 12 consultas MongoDB frente a 1 para los mismos 12 productos, datos idénticos, lotes de 200, peticiones repetidas, fotos eliminadas, prioridad de tallas, originales intactos, registro de cambios de precio y vuelta al modo anterior. El resultado mide consultas para ese escenario; no promete una reducción equivalente en el tiempo total de carga.

## Mediciones de navegación

Medición con la instancia de entrenamiento y los mismos datos, tras una carga de calentamiento por pantalla. Se han contado comandos SQL y aperturas de conexión en el servidor de prueba; no se añaden cabeceras de diagnóstico a la aplicación real.

| Petición | SQL anterior | SQL optimizado |
| --- | ---: | ---: |
| Una foto pública | 2 | 1 |
| Panel principal | 7 | 5 |
| Métricas | 20 | 13 |
| Listado de productos por AJAX | 3 | 2 |

El catálogo conserva sus cuatro comandos SQL de datos en este escenario y reduce aperturas de conexión de seis a cuatro. Las fotos son peticiones separadas y se contabilizan aparte; por ello una página con doce productos no significa una sola consulta SQL total. El lote de doce a una corresponde a las lecturas de imágenes de MongoDB. Los números dependen de sesión, pantalla y datos, y no equivalen a tiempos garantizados.

## Alcance pendiente

Esta fase no reduce el tamaño de los originales ni mueve las imágenes a la nube. Tampoco cambia todavía la carga completa del catálogo a una paginación SQL: la búsqueda predictiva, los descuentos y las familias requieren una preparación adicional para mantener los resultados. Antes de migrar o deduplicar archivos se deben medir tamaños y duplicados, hacer copias de los datos y comprobar una migración de prueba.
