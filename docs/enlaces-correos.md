# Dirección de los enlaces en los correos de pedidos

La confirmación de pedido, el reenvío de factura y el aviso al propietario construyen sus enlaces a partir del esquema, dominio, puerto y ruta base de la petición que origina el envío, igual que el restablecimiento de contraseña. No requieren cambiar `EmailSettings:BaseUrl` cada vez que se accede mediante localhost, Tailscale o un dominio propio.

Detrás de Tailscale Serve u otro proxy, el servidor debe reconocer las cabeceras reenviadas de ese proxy para identificar correctamente HTTPS y el dominio externo. Se usa `HttpContext.Request`, no las cabeceras sin validar directamente. Se conserva el perfil privado ya documentado para Tailscale.

`EmailSettings:BaseUrl` queda como alternativa para envíos que no tengan una petición HTTP, por ejemplo tareas de fondo. Debe apuntar al dominio externo correcto cuando se utilicen esas tareas.

El cambio se aplica a correos nuevos; los enlaces de correos ya enviados permanecen como estaban. Si el enlace es privado de Tailscale, el destinatario necesita acceso a esa red. Al usar un dominio público se utilizará ese dominio para los correos enviados desde él.

`dotnet run --project Tests/EmailLinkChecks` comprueba los enlaces de las tres plantillas en Tailscale, dominio propio, puerto alternativo, subruta, localhost y envíos sin petición. No conecta a SMTP ni envía correos reales.
