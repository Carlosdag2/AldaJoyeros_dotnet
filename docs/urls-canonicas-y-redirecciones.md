# URL canónicas, URL limpias y redirecciones 301

Revisión del 20 de octubre de 2026. Cambios aplicados en local; no se modifican productos, tallas, precios ni bases de datos.

## Resultado de la comprobación

| Elemento | Antes | Ahora |
|---|---|---|
| Canónicas | Existían en la plantilla SEO al configurar el dominio público. Usaban las rutas técnicas del catálogo y las fichas. | Utilizan las rutas públicas nuevas y el mismo generador que los enlaces y el sitemap. Se conserva una canónica por familia de producto y una por página/categoría del catálogo. |
| URL limpias | Rutas convencionales como `/Productos/Detalle/2792` y `/Home/Privacy`. | Rutas legibles como `/catalogo`, `/producto/anillos-oro-de-18-quilates-02000090` y `/privacidad`. El ejemplo de producto es ilustrativo: la categoría y la referencia reales determinan su texto. |
| Redirecciones 301 | No había una normalización de las direcciones públicas antiguas. HTTPS ya tenía su propia redirección. | Las direcciones antiguas, las diferencias de mayúsculas/barra final y las rutas anteriores con identificador redirigen de forma permanente a su dirección actual. |

## Para qué sirve cada cosa

**La canónica** indica la dirección preferida de una página a los buscadores. No cambia la pantalla ni impide abrir una talla. Por ejemplo, una ficha con `?talla=21&utm_source=correo` conserva la selección del cliente, pero su canónica apunta a la familia sin esos parámetros. Las páginas 2, 3, etc. del catálogo conservan canónicas distintas: no se envían todas a la primera página.

**Una URL limpia** facilita entender y compartir un enlace. La ruta de producto contiene únicamente un texto descriptivo de categoría y referencia: no muestra la clave numérica interna. Se pasa a minúsculas, se eliminan acentos y se sustituyen los separadores por guiones. Los ceros iniciales de las referencias comerciales se conservan. Las categorías también utilizan su nombre y las tallas utilizan su medida, sin `categoriaId` ni `tallaId` en las direcciones públicas.

**Una redirección 301** avisa al navegador y a los buscadores de que una página se ha trasladado permanentemente. Así los enlaces guardados no se rompen y la URL antigua deja de servir una segunda copia de la misma ficha. Google utiliza las redirecciones permanentes como señal de la dirección preferida, aunque puede decidir una canónica diferente.

## Rutas públicas

| Antigua | Preferida |
|---|---|
| `/Home`, `/Home/Index` | `/` |
| `/Productos`, `/Productos/Index` | `/catalogo` |
| `/Productos/Detalle/ID` | `/producto/texto-descriptivo` |
| `/Home/AvisoLegal` | `/aviso-legal` |
| `/Home/Privacy` | `/privacidad` |
| `/Home/Cookies` | `/cookies` |
| `/Home/Terminos` | `/condiciones-de-compra` |

Los filtros, las búsquedas y el paginado siguen usando parámetros cuando aportan información: `/catalogo/anillos?page=2` es una dirección válida y limpia. No todos los parámetros son perjudiciales ni hay que convertirlos en segmentos. Las búsquedas permanecen marcadas para no indexarse.

## Cómo se implementa

- `Extensions/PublicRouteExtensions.cs` registra las rutas públicas antes de las convencionales. Los enlaces generados por ASP.NET prefieren las rutas nuevas y las antiguas siguen siendo reconocidas.
- `Helpers/PublicUrls.cs` centraliza las direcciones, los textos descriptivos, la subcarpeta de despliegue y la conservación de parámetros relevantes.
- `Filters/PublicUrlFilter.cs` redirige únicamente la navegación pública GET/HEAD del inicio, catálogo y páginas informativas. No convierte los envíos de formularios, las API, el acceso o la administración en redirecciones permanentes.
- El detalle comprueba primero que la ficha exista. Las rutas antiguas con ID devuelven 301 a la ruta guardada. Un texto desconocido sin ID devuelve 404, sin adivinar a qué pieza corresponde. Una referencia de talla se lleva a su padre conservando la talla como `?talla=21`; las tallas inexistentes y los productos no disponibles siguen devolviendo 404.
- Los enlaces del inicio, listado, relacionados y sugerencias apuntan directamente a la ficha limpia. Se actualizan el selector de tallas, la dirección para compartir, JSON-LD y el sitemap.
- La navegación AJAX del catálogo actualiza tanto la dirección visible como la canónica al cambiar de página o categoría. La descripción de las rutas no modifica la búsqueda independiente de categorías.

Una redirección del acceso a la cuenta sigue siendo temporal: no significa que el pedido o el login se hayan trasladado para siempre. La redirección HTTPS existente conserva su comportamiento y no se cambian indiscriminadamente todos los 302/307 por 301.

## Direcciones estables y productos con nombres iguales

La nueva tabla MySQL `catalogo_urls` guarda los nombres públicos. Tiene una restricción de unicidad y reserva las direcciones bajo bloqueo de base de datos para evitar colisiones entre peticiones o instancias. Si dos piezas generan el mismo texto, la segunda recibe un sufijo como `-alternativa-2`; no es el ID de la base de datos. Una dirección reservada no se reutiliza para otra pieza cuando se oculta o retira un artículo.

Las direcciones no cambian al editar el nombre o la categoría. En las familias de Pérez Mora, la asignación se vincula a la referencia padre y no a la talla que representa temporalmente a la familia, por lo que no cambia al agotarse una talla. La tabla se crea automáticamente la primera vez, siguiendo el patrón de otras configuraciones del proyecto. El usuario MySQL necesita permiso de creación inicial y de lectura/inserción. No se cambian productos, precios ni imágenes.

Al trasladar las bases de datos a otro ordenador o alojamiento, **exportar también `catalogo_urls`** para conservar exactamente las direcciones ya publicadas. Los identificadores siguen usándose internamente para pedidos y relaciones, y las rutas privadas de administración conservan su funcionamiento.

## Local, Tailscale y futuro dominio

Las rutas y las redirecciones entre páginas funcionan ya en local y con Tailscale. El modo privado continúa sin indexación. No se obtiene el dominio canónico de una cabecera Host enviada por un visitante ni se inventa un dominio.

Para que las canónicas absolutas se publiquen, indicar en la configuración privada la dirección real del despliegue:

```text
Publication__PublicBaseUrl=https://www.tu-dominio-real.es
```

Cuando se publique y se quiera permitir indexación:

```text
Publication__IndexingEnabled=true
```

Existe además una opción independiente para enviar las visitas públicas al dominio preferido, por ejemplo de la versión sin www a la versión con www:

```text
Publication__RedirectToPublicOrigin=true
```

Esta última opción está **desactivada por defecto** y solo actúa con indexación habilitada y un dominio HTTPS válido. No hay que activarla en el uso privado con Tailscale. Antes de activarla, comprobar que el dominio apunta a la aplicación y que el proxy comunica correctamente el esquema HTTPS y el Host original; una configuración incorrecta del proxy puede provocar bucles. Si se despliega en una subcarpeta, incluirla en PublicBaseUrl y configurar PathBase correctamente. Reiniciar la aplicación tras modificar estas opciones.

## Comprobaciones

Se amplían las pruebas `Tests/PublicationChecks` para verificar URL descriptivas, referencias con ceros, normalización, subcarpetas, parámetros de talla, canónicas, generación de enlaces, redirecciones 301 de las rutas antiguas, peticiones HEAD y preferencia de dominio. Se mantienen las comprobaciones anteriores de consentimiento, acceso y seguridad.

```powershell
dotnet run --project Tests/PublicationChecks/PublicationChecks.csproj -p:StaticWebAssetsEnabled=false -p:GenerateJSModuleManifest=false
```

Además se verifica el catálogo real sin cambiar artículos, en una aplicación temporal sin tareas de proveedores. Solo se inicializa la tabla de direcciones de esta función. Se comprueban: enlaces directos, destino 200 sin otro salto, 301 de textos antiguos, conservación de la talla, 404 para fichas/tallas inexistentes, canónica y JSON-LD coincidentes, sitemap y sugerencias con las rutas nuevas, paginado AJAX y catálogo móvil sin desbordamiento.

## Fuentes

- [Google: consolidación de URL duplicadas y canónicas](https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls).
- [Google: redirecciones y búsqueda](https://developers.google.com/search/docs/crawling-indexing/301-redirects).
- [Microsoft: rutas de controladores y generación de URL](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0).
