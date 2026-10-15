# Informes de proveedores en PDF

Los enlaces del historial de Munreco y Pérez Mora descargan un PDF, tanto al comprobar cambios como al actualizar. El acceso sigue limitado al administrador y a los identificadores de ejecución del historial; no hay una descarga pública ni acceso libre a archivos del servidor.

El informe incluye el proveedor, origen de la tarea, fechas en hora de Madrid, estado y resumen de nuevas referencias, reposiciones, agotados, retiradas y modificaciones. Los listados detallan las referencias afectadas; se omiten las filas sin cambios, cuyo total se resume. Los productos retirados no se confunden con productos eliminados físicamente de la base de datos.

Una comparación indica expresamente que no modifica productos. El resultado de una actualización muestra su resumen y recupera la comparación de la misma ejecución para incluir las referencias. En Munreco se muestran además las acciones de visibilidad aplicadas y la conservación de los precios existentes. En Pérez Mora se indican los coeficientes disponibles, coincidencias pendientes e incidencias de imágenes. El informe de revisión contiene referencias y motivos; no publica direcciones privadas de descarga ni registros técnicos. Los errores se explican con instrucciones de recuperación sin exponer mensajes internos.

Los JSON originales se conservan como datos internos del proceso. No se vuelven a ejecutar importaciones ni se cambia el catálogo al descargar un informe. Las ejecuciones antiguas sin comparación siguen permitiendo obtener su resumen, indicando la ausencia del detalle.

La generación usa QuestPDF, que ya se utiliza en las facturas. No necesita instalar otro generador en el servidor. Los PDF tienen tablas con encabezados repetidos y numeración de páginas.

## Comprobaciones

`dotnet run --project Tests/SupplierReportChecks -- <carpeta-de-salida>` genera casos de comparación, resultado, revisión, incidencia, ausencia de cambios, históricos sin detalle y un listado de 180 referencias con precios y referencias largas. Son datos ficticios y no accede a las bases de datos ni al proveedor. Los archivos permiten comprobar visualmente la paginación y extraer el texto para verificar referencias completas y ausencia de datos técnicos sensibles.
