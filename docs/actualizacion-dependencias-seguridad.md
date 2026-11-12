# Dependencias y seguridad

Actualización verificada el 12 de noviembre de 2026.

- MailKit 4.14.1 → 4.18.2, que incorpora MimeKit 4.18.2 y System.Security.Cryptography.Pkcs 10.0.13.
- MongoDB.Driver 2.28.0 → 3.12.0, la misma versión que usan los tres importadores. Sus dependencias incluyen SharpCompress 0.48.1 y Snappier 1.3.1.
- Retirados AutoMapper 12.0.1 y su extensión de inyección. Las versiones corregidas requieren licencia desde la versión 15: las conversiones ahora están definidas explícitamente en `Mappings/AppMapper.cs`. Se preservan campos ignorados, contraseñas, relaciones, precios, totales y fechas.
- `Directory.Build.props` activa la auditoría de paquetes directos y transitivos para la web, las herramientas y las comprobaciones. No se suprimen avisos de seguridad.
- Se mantiene .NET 10 y la combinación EF Core/Pomelo 9; no se han actualizado indiscriminadamente las versiones mayores de otros paquetes.

## Comprobaciones

La auditoría de la solución no detecta vulnerabilidades conocidas:

```powershell
dotnet list AldaJoyeros.slnx package --vulnerable --include-transitive
```

Comprobaciones sin servicios externos (ejecutar desde la raíz):

```powershell
dotnet run --project Tests/MappingChecks
dotnet run --project Tests/PublicationChecks
dotnet run --project Tests/EmailLinkChecks
dotnet run --project Tests/GuestCartChecks
dotnet run --project Tests/AuthReturnChecks
dotnet run --project Tests/OrderNotificationChecks
dotnet run --project Tests/ProductVariantChecks
dotnet run --project Tests/TrafficChecks
dotnet run --project Tests/SupplierReportChecks -- RUTA_DE_SALIDA
```

MappingChecks cubre edición sin alterar identidades ni hashes, lectura de pedidos con referencias circulares, totales, imágenes existentes con el esquema BSON anterior y serialización MIME con factura adjunta. No envía correos ni cobra pagos. En la migración se compararon adicionalmente 42 conversiones contra el perfil anterior con resultados equivalentes.

Se han comprobado la compilación Debug y la publicación Release, así como los analizadores de Pérez Mora y de los tres CSV de Duward. En una instancia con bases de datos aisladas también se han verificado el inicio de sesión, los paneles de administración, el detalle de pedido, el catálogo con paginación AJAX, las galerías táctiles y la lectura, subida y eliminación de imágenes. La imagen temporal se eliminó al terminar. No se modificaron datos de producción.

Una auditoría limpia solo significa que NuGet no encuentra vulnerabilidades conocidas en las versiones consultadas; conviene repetirla antes de publicar.

## Aviso del SDK preliminar

NETSDK1057 indica que el equipo está usando un SDK preliminar (11.0.100-rc.3), no una vulnerabilidad de paquete. Instalar el SDK estable de .NET 10 con el instalador oficial/Visual Studio y seleccionar ese SDK resuelve ese aviso. No se ha instalado software ni cambiado el framework de la web como parte de esta actualización.

Fuentes: [MailKit](https://github.com/advisories/GHSA-9j88-vvj5-vhgr), [MimeKit](https://github.com/advisories/GHSA-g7hc-96xr-gvvx), [AutoMapper](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), [licencia de AutoMapper](https://docs.automapper.io/en/latest/15.0-Upgrade-Guide.html), [SharpCompress](https://github.com/advisories/GHSA-6c8g-7p36-r338), [Snappier](https://github.com/advisories/GHSA-pggp-6c3x-2xmx), [Pkcs](https://github.com/advisories/GHSA-846f-f764-76mx).
