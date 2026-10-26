# Carrito de invitado

En modo ecommerce cualquier visitante puede añadir artículos, elegir tallas, cambiar cantidades y eliminar o vaciar el carrito. El icono de la cabecera también está disponible sin cuenta. En modo escaparate se conserva el bloqueo de las funciones de compra.

Al continuar con la compra se pide iniciar sesión o crear una cuenta. Ambas opciones mantienen la ruta de checkout y transfieren las referencias del carrito temporal a la cuenta, combinando las cantidades si ya existían. Las líneas solo se retiran del temporal tras guardarse; si falla una transferencia se conservan los pendientes y se vuelve al carrito para reintentar. Los artículos que ya no están disponibles se retiran con un aviso. Se mantiene el límite de 99 unidades por referencia.

El carrito del invitado utiliza la sesión existente de ASP.NET (30 minutos de inactividad, almacenamiento en memoria). Solo contiene referencias y cantidades; los precios se consultan en el servidor. Una vez autenticado, el carrito se guarda en MySQL como antes. No se altera la autenticación JWT ni se permite completar pedidos sin cuenta.

Las mutaciones exigen protección antifalsificación. Las solicitudes de carrito y transferencia de una misma sesión se ejecutan en serie para evitar sobrescribir cantidades. No se necesitan cambios en las tablas de las bases de datos.

Pruebas sin servicios externos: `dotnet run --project Tests/GuestCartChecks/GuestCartChecks.csproj`. Cubren cantidades, tallas, combinación, reintentos, cambios de disponibilidad y vaciado.
