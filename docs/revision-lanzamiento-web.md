# Revisión de las 20 recomendaciones del vídeo

Revisión del vídeo `Download.mp4`, realizada el 20 de octubre de 2026. Los cambios se aplican al proyecto local. No publican la tienda, no importan productos ni modifican los datos del catálogo. WhatsApp, teléfono, dirección y los enlaces del negocio se mantienen como estaban, tal como has pedido.

## Qué se ha revisado y qué se ha hecho

| Nº | Recomendación | Situación y resultado |
|---|---|---|
| 1 | Aviso legal | Faltaba una página específica. Ahora existe y se enlaza desde el pie. Usa los datos de empresa configurados. El titular legal, NIF/CIF y datos registrales aplicables deben completarse y revisarse antes de publicar; no se inventan. |
| 2 | Privacidad | Había un texto genérico. Ahora explica cuentas, pedidos, estadísticas opcionales, derechos y proveedores externos. Su fecha de revisión es fija. Sigue pendiente concretar alojamiento, proveedores y conservación aplicables cuando se elijan. |
| 3 | Cookies | Había una política, pero se creaba una cookie analítica sin elección. Ahora se puede aceptar o rechazar con la misma facilidad, y retirar el permiso desde Cookies. Se han eliminado las afirmaciones de publicidad no instalada y los enlaces ficticios a instrucciones de navegadores. |
| 4 | HTTPS | Ya existían redirección a HTTPS y HSTS en producción. Se conservan. El certificado y la configuración del proxy se comprueban en el alojamiento definitivo. |
| 5 | Metatítulos y descripciones | Ahora hay descripción, título de producto con categoría, etiquetas para compartir, canonical y reglas de indexación. El dominio no se toma de localhost ni de una cabecera arbitraria. |
| 6 | Datos estructurados | Se añaden JSON-LD JewelryStore en inicio y Product en las fichas cuando se configura una dirección pública. No se inventan reseñas, coordenadas ni disponibilidad inmediata. No se prometen resultados enriquecidos: Google decide su presentación y la información de ofertas no se publica de forma ficticia. |
| 7 | Sitemap y robots | Nuevas rutas robots.txt, sitemap.xml y sitemap-N.xml. Se incluyen únicamente familias de productos visibles; las tallas no generan entradas duplicadas. Bloques de hasta 5.000 productos. Desactivados para indexación mientras el uso sea privado. |
| 8 | Ficha de Google | Se mantiene como está por tu indicación. Crear o verificar una ficha requiere la cuenta del propietario y no se hace desde el código de la tienda. |
| 9 | Favicon | El archivo ya existía, pero no se enlazaba desde la plantilla general. Ahora sí se utiliza. |
| 10 | Texto alternativo | Mejorados los textos del catálogo con categoría y referencia, añadidos los de las miniaturas y la previsualización de administración. La imagen decorativa del inicio tiene alt vacío. |
| 11 | Imágenes comprimidas | Las imágenes públicas del catálogo se entregan en WebP, calidad 82 y hasta 800 px en su lado mayor, respetando proporción y orientación. Se conservan los originales en MongoDB para administración y otros usos. |
| 12 | Carga optimizada | El HTML ya no incluye las fotos del catálogo codificadas en Base64. Se consultan metadatos y se solicitan las fotos aparte, con caché y ETag. La conversión dispone de caché limitada a 32 MB y dos conversiones simultáneas. Tailwind se compila en un CSS local de aproximadamente 89 KB, en vez de compilarse en el navegador con el CDN. No se afirma que toda la web haya superado una auditoría de rendimiento en un alojamiento real. |
| 13 | Contraste | Se mantienen los temas claro, oscuro y alto contraste. Las nuevas páginas se adaptan a ellos. Esto no equivale a una certificación completa WCAG de todas las pantallas existentes. |
| 14 | Móvil | Se mantiene Tailwind y se ha comprobado la nueva política a 320, 768 y 1.440 px, sin desbordamiento horizontal. Se han revisado capturas en los tres temas. |
| 15 | Página 404 | Ahora hay una pantalla de error con acceso al catálogo. Devuelve HTTP 404; las fichas inexistentes ya no redirigen como si fueran páginas válidas. Los estados de acceso denegado no se convierten en 404. |
| 16 | Enlaces rotos | Eliminados los enlaces ficticios de la política de cookies y comprobados los destinos públicos modificados. No se ha hecho un rastreo exhaustivo de todos los enlaces de terceros ni de las pantallas privadas con pedidos reales. |
| 17 | Formularios contra spam | Los formularios de acceso, registro y recuperación validan el token antifalsificación y admiten como máximo 10 envíos por minuto, por acción e IP. Se informa de la espera con HTTP 429. Se conserva el límite existente de intentos del código de recuperación. No se añade un CAPTCHA externo. |
| 18 | WhatsApp visible | Se conserva el enlace actual del pie y su número, sin añadir un botón flotante ni cambiar el contacto. |
| 19 | Analítica | Ya existían estadísticas internas. Ahora solo registran visitas tras aceptar analítica. No se añade Google Analytics ni otro rastreador. Las estadísticas actuales son temporales: se mantienen en memoria y se reinician al parar la aplicación. |
| 20 | Una llamada a la acción | La cabecera principal de inicio se centra en Explorar Catálogo. Se retira el segundo botón que competía con el primero. La navegación, los botones de compra y los controles necesarios de otras pantallas se mantienen. |

## Por qué y cómo funcionan los cambios principales

**Cookies:** mostrar una política no basta para ofrecer una elección efectiva. La aplicación consulta `.AldaJoyeros.Consent` antes de registrar una visita. Por defecto no crea `.AldaJoyeros.Visitor`; si retiras el permiso, la elimina. La elección dura 180 días, es del servidor y sus cambios se protegen contra formularios falsificados. Las funciones necesarias para sesión, carrito y compra siguen funcionando al rechazar analítica.

**SEO y entorno privado:** una tienda privada con Tailscale no debe anunciar enlaces a localhost ni tratarse como una tienda pública. La indexación viene desactivada. Robots y las etiquetas noindex expresan esa preferencia, pero no sustituyen a Tailscale, la autenticación ni los controles de permisos. Al publicar, se activa de forma expresa y se indica el dominio HTTPS correcto.

**Imágenes:** insertar fotos Base64 agranda cada página y obliga a transferirlas de nuevo con el contenido. Ahora las fichas y el listado incluyen direcciones de imagen; el navegador puede reutilizarlas y recibir un 304 cuando no cambian. El servidor comprueba que el producto siga visible antes de responder y limita el tamaño de entrada y de decodificación. No se reescriben las fotos del proveedor. SkiaSharp y sus recursos nativos se incluyen también para despliegues Linux, aunque las pruebas de esta revisión se han ejecutado en Windows.

**Tailwind:** sigue siendo el sistema de estilos de la web. Lo que cambia es cuándo se genera el CSS: se entrega ya compilado, en lugar de descargar el compilador de desarrollo y ejecutarlo en cada dispositivo. El archivo generado está incluido en el proyecto; Visual Studio y el servidor no necesitan Node para arrancar la tienda. Cuando se añadan clases nuevas a las vistas, regenerarlo desde la carpeta del proyecto:

```powershell
npm ci
npm run build:css
```

Para trabajar continuamente en las vistas se puede usar `npm run watch:css`. `node_modules` no se incorpora a Git ni al contenido de la aplicación.

**Formularios:** el límite de frecuencia dificulta el envío automatizado y los intentos repetidos; la validación antifalsificación impide que otra página envíe formularios válidos en nombre del usuario. Son medidas distintas, por eso se aplican ambas. No garantizan eliminar todo el abuso. Si el alojamiento usa un proxy, hay que configurar correctamente sus direcciones de confianza para que el límite distinga las IP de los visitantes.

## Cuando se publique con un dominio

Estas variables son ejemplos: sustituir los valores por los reales en la configuración privada del alojamiento. No hay que activarlas en el uso privado actual.

```text
Publication__IndexingEnabled=true
Publication__PublicBaseUrl=https://www.tu-dominio-real.es
Publication__LegalOwner=Nombre o razón social del titular real
Publication__RegistryDetails=Datos registrales, si proceden
EmpresaSettings__Cif=NIF o CIF real
```

Si se instala en una subcarpeta, incluirla en PublicBaseUrl y configurar PathBase en el proxy/aplicación. Robots debe poder consultarse desde la raíz del dominio: en un despliegue por subcarpetas puede ser necesario configurarlo en el proxy. Comprobar después el certificado HTTPS, la URL canonical, el sitemap y el tratamiento de cabeceras del proxy. Añadir el sitemap en Search Console requiere la cuenta y la verificación del dominio.

Completar la identidad fiscal y revisar los textos legales, proveedores, plazos de conservación, transferencias y condiciones de venta según la actividad real. Esta revisión técnica deja las páginas preparadas, pero no sustituye esa comprobación ni acredita por sí sola cumplimiento legal.

## Verificación y límites

Pruebas repetibles sin servicios externos:

```powershell
dotnet run --project Tests/PublicationChecks/PublicationChecks.csproj -p:StaticWebAssetsEnabled=false -p:GenerateJSModuleManifest=false
```

Se han superado 49 comprobaciones de configuración pública/privada, cookies y retirada de permiso, protección del login real, límite de envíos, 404, canonical, sitemap de páginas y conversión WebP. Las pruebas no utilizan bases de datos ni servicios de correo o pago. Las opciones de recursos estáticos de ese comando evitan un problema del SDK 11 instalado en este equipo con proyectos auxiliares; no desactivan los recursos de la aplicación principal.

Además se han realizado pruebas temporales de navegador sobre el catálogo real en lectura: JSON-LD válido, fotos WebP con ETag y respuesta 304, primer bloque del sitemap de 5.000 familias sin duplicados, navegación pública, textos alternativos y diseño responsive. Se han revisado capturas de las nuevas pantallas. La aplicación de pruebas se detiene al terminar; no se deja la tienda arrancada en segundo plano.

El proyecto ya presentaba advertencias NuGet de seguridad que requieren una actualización y pruebas propias. La herramienta de compilación Tailwind 3 también tiene avisos en dependencias de desarrollo; no se instala ni se ejecuta en el navegador o servidor de la tienda. Se conserva la misma generación de Tailwind para evitar una migración visual mayor dentro de esta revisión. Antes de exponer la web públicamente deben revisarse estas dependencias. No se han hecho actualizaciones forzadas de paquetes ajenos a los cambios.

## Referencias

- [AEPD: guía sobre cookies](https://www.aepd.es/guias/guia-cookies.pdf).
- [LSSI, artículo 10: información del prestador](https://www.boe.es/buscar/pdf/2002/BOE-A-2002-13758-consolidado.pdf).
- [Google: datos estructurados de negocios locales](https://developers.google.com/search/docs/appearance/structured-data/local-business).
- [Google: creación de sitemaps](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap).
- [Tailwind: el CDN está pensado para desarrollo](https://v3.tailwindcss.com/docs/installation/play-cdn).
- [Tailwind: optimización para producción](https://v3.tailwindcss.com/docs/optimizing-for-production).
