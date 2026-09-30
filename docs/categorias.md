# Organización del catálogo

Las categorías se agrupan en Joyas, Relojería, Complementos y Colecciones especiales. La página pública muestra únicamente categorías con productos visibles; las referencias pendientes de revisión siguen disponibles en administración.

Las categorías repetidas por material o género se reúnen por tipo de pieza: Anillos, Pulseras, Pendientes, Collares, etc. Se mantienen las colecciones de bebé, comunión, boda, diamantes y oro de 9 quilates porque sus productos abarcan varios tipos. Los atributos y las colecciones originales del proveedor se conservan en las fichas.

`Helpers/CatalogTaxonomy.cs` contiene las reglas compartidas por la web y los importadores de Munreco y Pérez Mora. La reorganización también actualiza la categoría de referencia de la sincronización de Pérez Mora, conservando las decisiones manuales.

La herramienta `Tools/CatalogCategories` utiliza la configuración local sin mostrar conexiones. `--plan` previsualiza; `--apply` guarda un respaldo en `App_Data/CategoryOrganization`, aplica una transacción y comprueba que todos los datos de productos, salvo su categoría, permanecen iguales. Bloquea importaciones concurrentes. Las imágenes de MongoDB no se modifican.

La primera reorganización local pasó de 77 a 34 categorías, con 33 categorías públicas, conservando los 27.814 productos.
