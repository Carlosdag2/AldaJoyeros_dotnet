# Productos con tallas

El catálogo público agrupa las referencias de Pérez Mora con formato `referencia/talla` numérica, por ejemplo `02000090/09` y `02000090/10`, en una sola ficha `02000090`. Las referencias con barra de otros proveedores permanecen independientes.

El catálogo, la búsqueda y sus sugerencias, los destacados, los relacionados y la presentación en tienda muestran una sola entrada por familia. La agrupación se realiza antes de paginar y limitar sugerencias. Buscar una referencia completa de talla sigue encontrando su familia. El precio «Desde» es el menor precio de las tallas visibles, nunca el importe genérico del padre ni el precio de una talla oculta.

En el detalle hay que elegir una talla. La selección recarga la ficha con el precio, peso, características e imágenes correspondientes a esa variante y muestra su referencia completa. Las imágenes pueden heredarse de otro registro visible de la familia cuando la variante no tiene foto. Elegir otra talla conserva la cantidad predeterminada de una unidad.

En modo tienda, los accesos rápidos de una familia llevan a «Elegir talla». El botón de compra permanece desactivado hasta seleccionarla. El servidor rechaza añadir un padre con tallas o un producto oculto. El carrito almacena el ID de la variante y toma su precio de MySQL; cada talla conserva su propia línea. Pedidos, facturas e importaciones mantienen las referencias completas y los registros originales.

No requiere migraciones, renombrados ni eliminación de datos en MySQL o MongoDB. Administración sigue mostrando las referencias individuales para gestionarlas. Las nuevas tallas y los cambios de precio se reflejan con los datos disponibles al consultar la web.

Comprobación de las reglas: `dotnet run --project Tests/ProductVariantChecks/ProductVariantChecks.csproj`.
