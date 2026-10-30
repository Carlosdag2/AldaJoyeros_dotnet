# Duward en el panel de administración

En **Admin → Actualización de proveedores → Duward** se pueden seleccionar uno o varios CSV originales de Sport, Aquastar y Lady.

1. Selecciona los archivos, sin cambiar sus columnas.
2. Pulsa **Comprobar archivos**. La comprobación valida productos e imágenes, pero no cambia el catálogo de la tienda.
3. Consulta **Cambios detectados (PDF)** y **Datos e imágenes para revisar (PDF)** en el historial.
4. Selecciona los mismos archivos y pulsa **Importar / actualizar catálogo** para guardar los cambios.
5. Consulta el resultado PDF cuando termine. El trabajo sigue en segundo plano aunque cierres el panel; la aplicación debe mantenerse encendida.

## Datos y actualizaciones

- La referencia completa `CodigoArticulo` identifica cada producto de Duward. Subir de nuevo la misma referencia actualiza su ficha; no crea otra.
- `PrecioVentaconIVA2` es el PVP. `PrecioVentasinIVA1` no se publica como precio de venta. Se leen decimales con coma, UTF-8 y Windows-1252.
- `UnidadSaldo` indica las existencias del proveedor. Los agotados y las fichas sin una imagen válida quedan ocultos para revisión, respetando las decisiones manuales existentes.
- Los productos se organizan en **Relojes → Duward · Sport / Duward · Aquastar / Duward · Lady**. Marca, colección, género, medidas válidas, peso, código alternativo completo y referencia de pareja se incluyen en las características cuando existen. Los datos originales se conservan completos, sin exponer el precio sin IVA en las características públicas.
- Una medida incoherente no se convierte por suposición: se omite de las características públicas y se explica en el PDF de revisión. Un código alternativo en notación científica tampoco se transforma en un EAN inventado.
- Se mantienen precios, descripciones, categorías y decisiones de visibilidad que el administrador haya modificado manualmente. Las referencias coincidentes con productos no vinculados a Duward se dejan para revisión; no se fusionan automáticamente.

## Colecciones incompletas y retiradas

Por defecto solo se añaden o actualizan las referencias incluidas. No se retiran artículos por faltar en un archivo.

La opción **Ocultar referencias que ya no aparecen en estas colecciones** solo se debe marcar cuando cada colección seleccionada esté completa. Una carga de Sport no retira Lady ni Aquastar. Una retirada superior al 15% del catálogo anterior incluido (con una tolerancia de cinco referencias) se bloquea para revisar los archivos.

## Imágenes

Primero se intenta el enlace `sysFichero`. Cuando falla, se consulta el catálogo público oficial de Duward y se exige la coincidencia de SKU y del nombre de imagen con la referencia completa. También se comprueban formatos alternativos de esa misma referencia en su CDN oficial. No se utiliza otro color o modelo parecido.

Se aceptan únicamente los dominios de Duward y su espacio verificado en Shopify. Las descargas tienen límites de tamaño, tiempo y concurrencia; se comprueba que los archivos se puedan decodificar como imágenes. Los archivos ya validados y los errores 404 se almacenan en una caché de 15 minutos para evitar peticiones repetidas. Se guardan en MongoDB siguiendo la estructura y los índices de la tienda y se reutilizan las imágenes ya existentes por contenido.

Si no se consigue ninguna imagen válida, el artículo queda para revisión y no se publica automáticamente. El PDF indica las imágenes recuperadas y las referencias pendientes.

Los CSV son la fuente de precios y existencias. La web pública se usa para las imágenes. Para actualizar el catálogo hay que subir un CSV nuevo del proveedor; no se programa una repetición de un archivo antiguo como si fuese un catálogo nuevo.

## Instalación y funcionamiento

Se usan las conexiones MySQL y MongoDB existentes, sin nuevas credenciales. Las tablas `duward_jobs` y `duward_product_sync` y las correspondencias en `producto_proveedor` se preparan automáticamente. Los archivos y los informes se guardan fuera de `wwwroot`.

Al compilar y publicar la web se incluye `DuwardRunner`, con el importador y sus dependencias. No hay que lanzar una consola manualmente. El bloqueo de ejecución depende del nombre de la base de datos y evita dos tareas simultáneas de Duward sobre la misma base.

Configuración opcional del servidor (sin contraseñas):

```json
"Duward": {
  "WorkerEnabled": true,
  "DataDirectory": "App_Data/Duward",
  "DotnetPath": "dotnet"
}
```

`RunnerPath` solo es necesario si el servidor instala el importador en una ruta distinta. La cuenta del servidor necesita acceso de escritura a la carpeta privada y salida HTTPS a Duward y a su CDN. Si la aplicación se reinicia durante un trabajo, el historial lo marca como interrumpido; se puede revisar y reintentar sin duplicar los productos ya vinculados.

El panel y sus informes requieren una cuenta administradora. Las acciones de carga también validan el token de protección de formularios.

## Si Visual Studio no encuentra el importador

Abre `AldaJoyeros.slnx`, con todos sus proyectos cargados. La solución debe mostrar la web y los importadores MunrecoSync, PerezMoraSync y DuwardSync. Después de detener la web, utiliza **Compilar → Recompilar solución** y vuelve a iniciarla.

En una copia anterior donde DuwardSync no aparezca, pulsa con el botón derecho sobre la solución, **Agregar → Proyecto existente**, y selecciona `Tools/DuwardSync/DuwardSync.csproj`. Guarda la solución y recompílala.

La compilación copia `DuwardSync.dll`, `DuwardSync.deps.json`, `DuwardSync.runtimeconfig.json` y sus dependencias a `DuwardRunner`, dentro de la salida de la web. Si faltan los archivos principales, la compilación se detiene con un mensaje explicativo para evitar una web compilada sin importador. No hace falta ejecutar DuwardSync como proyecto de inicio; el proyecto de inicio sigue siendo AldaJoyeros.