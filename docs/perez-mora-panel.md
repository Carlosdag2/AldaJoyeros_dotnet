# Pérez Mora en el panel de administración

Acceso: `/AdminPerezMora`, desde «Catálogo de Pérez Mora» en el panel principal. Requiere rol ADMIN. Todos los formularios que cambian datos tienen protección antifalsificación. Los Excel, enlaces de cuenta, imágenes de trabajo, respaldos e informes se guardan en `App_Data/PerezMora`, fuera de `wwwroot` y excluidos de Git y de la publicación.

## Uso

- **Comprobar cambios** descarga un Excel actual y prepara un informe sin cambiar productos ni imágenes.
- **Actualizar ahora** vuelve a descargar el catálogo, valida sus datos e imágenes y sincroniza MySQL y MongoDB. El historial muestra el progreso y permite descargar los informes.
- **Subir un Excel** acepta el `.xls` original del proveedor. Por defecto solo compara; marcar «Aplicar los cambios a mi catálogo» solicita la importación. No se ejecutan macros, fórmulas, scripts ni instrucciones contenidas en el archivo.
- **Programación semanal** permite elegir día y hora de Madrid. La cola y la programación se conservan en MySQL. Si la web estaba apagada, realiza una revisión pendiente al volver a arrancar, sin acumular una tarea por cada semana ausente.
- **Conectar la descarga** valida el enlace HTTPS que aparece en el botón del Excel dentro de la cuenta profesional de Pérez Mora. Lo guarda cifrado mediante ASP.NET Data Protection y nunca lo vuelve a mostrar. No guarda la contraseña del proveedor. Si cambia el enlace, copiarlo de nuevo desde la página «Descargas».

El enlace oficial utiliza el servicio `perezmorajewelry.com`. Se comprobó que permite descargar desde el servidor el mismo catálogo que desde la sesión B2B. No se comparten cookies ni contraseñas entre dominios. El servidor solo admite ese dominio y la ruta oficial de exportación; bloquea redirecciones y limita el archivo a 30 MB.

## Formato y datos

El `.xls` del 30/09/2026 contiene una tabla HTML codificada en Windows-1252. El lector comprueba las 19 columnas, el final de la tabla, los tipos de precio/stock y la unicidad de las referencias antes de escribir. Si el proveedor cambia el formato, se requiere adaptar el lector; no se interpreta una página de inicio de sesión como un catálogo vacío.

Se conservan las referencias como texto, incluidos ceros iniciales y sufijos de talla. Una referencia como `0024/14` es distinta de `0024`. Cuando existe la referencia padre explícita `0024`, se pueden heredar categoría, subcategoría, descripción o imágenes ausentes en la variante. Las columnas originales se conservan completas en `producto_proveedor.datos_json`; metal, color, peso, piedras, medidas, cierre, talla y género quedan disponibles también como características para la ficha pública.

`PRECIO` se interpreta como **coste del proveedor**. El panel permite previsualizar y guardar un coeficiente entre 0,01 y 100, con hasta cuatro decimales. El PVP es `coste × coeficiente`, redondeado a dos decimales (mitades hacia arriba). El coeficiente inicial es 1 y no añade impuestos por separado.

Guardar el coeficiente recalcula los precios automáticos existentes y establece la regla para futuras importaciones manuales, mediante Excel y programadas. Siempre se utiliza el coste original, nunca el PVP anterior; repetir la operación no acumula multiplicadores. Se conservan los precios editados manualmente, otras marcas, imágenes, categorías y visibilidad. El coste del Excel sigue guardado en `datos_json.PRECIO` y en la columna histórica `producto_proveedor.pvp` (para Pérez Mora esta columna contiene el coste). La configuración persistente se guarda en `perez_mora_pricing` y los respaldos de cada recálculo en `App_Data/PerezMora/pricing`, fuera de la web pública. El recálculo comparte el bloqueo del importador y actualiza el precio de referencia de la sincronización en una transacción.

Los productos con stock cero, sin categoría, sin descripción, sin imágenes válidas o sin coste positivo quedan ocultos para revisión. Las reposiciones reactivan productos ocultados automáticamente; las decisiones manuales de visibilidad se mantienen. Las retiradas se aplican solamente a productos de este proveedor después de procesar el catálogo completo. Una desaparición superior al 15% exige revisión y detiene la aplicación de cambios.

## Identidad e imágenes

La identidad se vincula al proveedor y a la referencia completa. Se utiliza un identificador externo estable derivado de la referencia, para preservar diferencias como N/Ñ pese a la intercalación de la clave externa heredada. La referencia visible se guarda intacta. Si una referencia nueva coincide con un producto existente sin vínculo de Pérez Mora, se informa como coincidencia pendiente y no se crea ni se fusiona automáticamente.

Las imágenes se descargan exclusivamente de las rutas `/fotos/` HTTPS de `joseperezmora.es`, con un máximo de 5 MB por imagen, tres solicitudes simultáneas y comprobación del formato. Las revisiones consultan las imágenes de nuevo, con peticiones condicionales cuando el servidor lo permite, para detectar sustituciones aunque la URL se conserve. Las descargas fallidas se registran y dejan el producto oculto para revisión.

El informe «Productos ocultos y revisión» identifica referencias sin existencias, sin categoría, sin coste, sin descripción o con enlaces que no entregan imágenes válidas. El Excel recibido incluye también enlaces `.mp4` en columnas de imagen: se conservan como datos del proveedor, pero no se introducen como imágenes en MongoDB.

En MongoDB, los identificadores de las imágenes importadas son deterministas por producto y contenido. Se evita duplicar imágenes de un producto aunque se repita la importación o se interrumpa una ejecución. Se sustituyen únicamente imágenes gestionadas por Pérez Mora y se conservan imágenes añadidas manualmente. El sistema registra el vínculo antes de dar por completado cada producto y verifica los vínculos finales.

## Despliegue

La compilación/publicación de `AldaJoyeros.csproj` incluye `PerezMoraRunner/PerezMoraSync.dll` y sus dependencias. El servidor necesita .NET 10 y permiso para iniciar ese proceso. Se crean automáticamente las tablas `perez_mora_jobs`, `perez_mora_schedule`, `perez_mora_pricing` y `perez_mora_product_sync`, y se utiliza la tabla de metadata `producto_proveedor` existente. El usuario MySQL necesita permisos para crear las tablas necesarias.

```json
{
  "PerezMora": {
    "WorkerEnabled": true,
    "DataDirectory": "App_Data/PerezMora",
    "DotnetPath": "dotnet"
  }
}
```

Las conexiones se toman de la configuración privada habitual de la web y se pasan al proceso en su entorno, sin escribirlas en argumentos ni informes. Se requieren almacenamiento persistente y permisos privados para `App_Data/PerezMora` y `App_Data/DataProtectionKeys`; conservar las claves permite abrir el enlace cifrado. El alojamiento debe mantener la web en ejecución para realizar las tareas programadas. Los archivos locales de configuración no se incluyen en la publicación.

## Verificación

```powershell
dotnet build AldaJoyeros.csproj -c Release
dotnet Tools/PerezMoraSync/bin/Release/net10.0/PerezMoraSync.dll --analyze RUTA_DEL_EXCEL.xls
dotnet run --project Tests/PerezMoraChecks/PerezMoraChecks.csproj -c Release -- RUTA_DEL_PROYECTO
```

Las pruebas de importación crean bases MySQL y MongoDB temporales con nombre aleatorio y las eliminan al terminar. Incluyen repetición de la importación, ausencia de duplicados, ceros iniciales, variantes, N/Ñ, retiradas, reposiciones, cambios manuales, sustitución de imágenes y conservación de fotos manuales. Las pruebas del panel (`--web`, instancia local en puerto 5091) comprueban permisos ADMIN, antifalsificación, programación inválida y visualización móvil.

Análisis del Excel recibido: 25.098 referencias únicas, 3.789 con stock cero, 12.682 URL de imágenes distintas y 1.046 referencias para revisión después de heredar datos de padres explícitos. Comparación real completada: 25.098 nuevas y cero colisiones con el catálogo existente. Las comprobaciones de importación y panel se superaron. La programación semanal se conserva desactivada a petición del usuario. La compilación conserva los cinco avisos previos de dependencias de la aplicación.

Primera importación real completada el 30/09/2026: 25.098 referencias incorporadas, 35.900 imágenes vinculadas en MongoDB y 27.814 productos totales en MySQL. Se verificó que los 2.716 productos anteriores conservaran íntegramente sus datos. Se mantienen ocultos 4.438 productos de Pérez Mora: 3.789 sin stock y 1.470 para revisión, con casos solapados. Hubo 102 enlaces sin imagen compatible, incluidos 32 vídeos `.mp4`; están identificados en los informes. La verificación final no encontró duplicados de producto ni incoherencias en el orden de imágenes o en sus vínculos. Se comprobó la ficha pública con características e imágenes y el catálogo tras la importación. Compilación y publicación local correctas, sin archivos privados en el paquete publicado.
