# Avisos de nuevos pedidos al propietario

Al finalizar una compra y guardar el pedido, la web envía al propietario un correo con el número de pedido, la fecha, el email del cliente, los artículos, las cantidades, el total y el método de pago. El botón **Gestionar pedido** abre el detalle del pedido en el panel y requiere iniciar sesión como administrador.

## Destinatario

Por defecto se utiliza `EmailSettings:SenderEmail`, el correo remitente de la tienda que ya está configurado. Para enviar los avisos a otro buzón, añade `OwnerEmail` dentro de `EmailSettings` en `appsettings.Local.json`:

```json
{
  "EmailSettings": {
    "OwnerEmail": "propietario@example.com"
  }
}
```

También se puede establecer la variable de entorno `EmailSettings__OwnerEmail`. Si el valor está vacío, se utiliza el remitente. Reinicia la aplicación después de cambiar la configuración.

`EmailSettings:BaseUrl` debe apuntar a la dirección de la web; al desplegar, utiliza el dominio público para que el enlace del correo abra el panel correcto.

## Cuándo se envía

- Tarjeta: después de verificar el pago y crear el pedido.
- Contra reembolso: después de crear el pedido; el aviso indica que está pendiente de cobro al entregar.
- Un pago fallido o un pedido que no se guarda no generan el aviso.
- Consultar un pedido o reenviar su factura no genera otro aviso de nuevo pedido.

El aviso al propietario se intenta aunque falle el PDF o el correo del cliente. Si falla el envío del aviso, el pedido sigue confirmado y el error queda en los registros de la aplicación con el número de pedido. No hay reintento automático.

## Comprobaciones

`Tests/OrderNotificationChecks` comprueba el flujo con servicios simulados, sin acceder a SMTP, Stripe ni bases de datos. Incluye tarjeta, contra reembolso, fallos de ambos correos y del PDF, pagos rechazados y repetición del formulario después de completar el pedido.
