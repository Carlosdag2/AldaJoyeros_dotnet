# Organización del catálogo

Las categorías se agrupan en Joyas, Relojería, Complementos y Colecciones especiales. La página pública muestra únicamente categorías con productos visibles; las referencias pendientes de revisión siguen disponibles en administración.

Las categorías repetidas por material o género se reúnen por tipo de pieza: Anillos, Pulseras, Pendientes, Collares, etc. Se mantienen las colecciones de bebé, comunión, boda, diamantes y oro de 9 quilates porque sus productos abarcan varios tipos. Los atributos y las colecciones originales del proveedor se conservan en las fichas.

`Helpers/CatalogTaxonomy.cs` contiene las reglas compartidas por la web y los importadores de Munreco y Pérez Mora. La reorganización también actualiza la categoría de referencia de la sincronización de Pérez Mora, conservando las decisiones manuales.

La herramienta `Tools/CatalogCategories` utiliza la configuración local sin mostrar conexiones. `--plan` previsualiza; `--apply` guarda un respaldo en `App_Data/CategoryOrganization`, aplica una transacción y comprueba que todos los datos de productos, salvo su categoría, permanecen iguales. Bloquea importaciones concurrentes. Las imágenes de MongoDB no se modifican.

La primera reorganización local pasó de 77 a 34 categorías, con 33 categorías públicas, conservando los 27.814 productos.

## Subcategorías por tipo y material

El catálogo presenta familias desplegables, con «Ver todos» y subcategorías específicas. Por ejemplo: Anillos → Plata, Acero, Oro de 18 quilates, Sellos, Solitarios y Tresillos; Pendientes → Tornillería o bebé, Comunión y Señora; Pulseras → Esclavas y Rígidas, también por material.

La clasificación usa el material declarado y la descripción del producto. No interpreta un baño de oro como oro macizo, no inventa materiales cuando faltan datos y mantiene las colecciones especiales. Las categorías pendientes de revisión y los productos ocultos no se hacen públicos.

Los importadores de Munreco y Pérez Mora usan las mismas reglas para los productos nuevos. Las categorías originales y las características del proveedor se conservan en sus metadatos. Los cambios manuales de categoría registrados por la sincronización de Pérez Mora se respetan.

Para aplicar esta distribución en otra base de datos, ejecutar la herramienta con la configuración privada de ese entorno:

```powershell
dotnet run --project Tools/CatalogCategories -- . --specific --plan
dotnet run --project Tools/CatalogCategories -- . --specific --apply
```

La opción `--apply` crea un respaldo en `App_Data/CategoryOrganization/specific-*` y realiza la reasignación en una transacción. Conserva las categorías generales y sus identificadores para que los enlaces antiguos sigan funcionando. Los filtros generales incluyen sus subcategorías; los filtros específicos incluyen únicamente los productos asignados a ellas. Se actualiza la categoría de referencia de la sincronización sin cambiar las preferencias manuales.

La aplicación local reasignó 17.418 productos y verificó que los 27.814 productos conservaban todos sus datos salvo `categoria_id`. MongoDB no recibió cambios. Repetir el plan tras aplicarlo devuelve cero modificaciones.

## Pendientes por destinatario

Los pendientes se agrupan en tres subcategorías, sin subdivisiones adicionales por material. La comunión indicada en la categoría o los datos originales tiene prioridad; los pendientes de bebé o con cierre de rosca, tuerca o tornillo se incluyen en «Tornillería o bebé»; los de niña o infantil en «Comunión», y los restantes en «Señora». Una categoría genérica «rosca y presión» no implica que todas sus piezas tengan cierre de rosca. Los conjuntos de varias piezas conservan su categoría. Las características y el material siguen en la ficha.

Los importadores aplican estas reglas a nuevas referencias. Pérez Mora aporta también su categoría, subcategoría y género originales. Para reorganizar únicamente los pendientes existentes, respetando categorías manuales y guardando un respaldo:

```powershell
dotnet run --project Tools/CatalogCategories -- . --earrings --plan
dotnet run --project Tools/CatalogCategories -- . --earrings --apply
```

La reorganización local del 12 de octubre de 2026 asignó 3.136 pendientes: 550 a Tornillería o bebé, 131 a Comunión y 2.455 a Señora. Se conservaron los 27.814 productos y todos sus datos salvo las categorías modificadas. El plan posterior devolvió cero cambios; las categorías anteriores sin productos dejan de mostrarse en los filtros públicos.
