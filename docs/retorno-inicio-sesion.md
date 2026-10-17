# Continuar al enlace tras iniciar sesión

Las páginas protegidas redirigen a Login con `returnUrl`, incluyendo la ruta base y los parámetros de consulta. El formulario conserva este destino en un campo oculto y lo mantiene al reintentar con credenciales erróneas o al pasar a Crear cuenta.

Después de iniciar sesión o registrarse se vuelve a la ruta solicitada. Si ya había sesión y se abre Login o Register con un destino válido, se continúa directamente a esa ruta. Los destinos externos se descartan y las redirecciones de retorno son locales. Sin destino se mantienen las pantallas habituales de inicio o administración.

Las comprobaciones de propiedad del pedido y del rol siguen realizándose en la página de destino. Conservar el enlace no concede permisos adicionales ni altera el funcionamiento del JWT.

Pruebas: `dotnet run --project Tests/AuthReturnChecks`. Comprueban retorno al pedido, consultas y subrutas, errores de login, registro, sesión existente, destinos externos y permisos. No acceden a bases de datos ni envían correos.
