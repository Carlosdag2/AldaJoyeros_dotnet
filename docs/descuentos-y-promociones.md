# Descuentos y promociones

Acceso: **Admin → La imagen de tu tienda → Descuentos y promociones**.

## Crear y modificar ofertas

1. Pulsa «Crear descuento» y elige una categoría. Para un artículo, selecciona «Un producto y sus tallas» y «Buscar y elegir producto con imagen»: busca por nombre/referencia, filtra por categoría y pulsa su tarjeta. También puedes hacerlo desde la ficha pública del producto siendo administrador.
2. Indica nombre y porcentaje (0,01–99,99 %). Una categoría principal incluye sus subcategorías; una oferta del padre incluye las tallas de Pérez Mora.
3. Revisa los precios anteriores y confirma la referencia de los 30 días antes de activar la oferta. La web no puede reconstruir precios anteriores al comienzo del registro: esa comprobación inicial corresponde al administrador.
4. Puedes modificar, pausar, reactivar o eliminar cada oferta. No se modifican los PVP base, costes, coeficientes ni datos de los proveedores. Las importaciones siguen actualizando esos datos y los descuentos se calculan aparte.

El porcentaje configurado se aplica siempre al PVP base: 49 € con un 50 % se queda en 24,50 €, aunque antes se haya probado un 12 %, un 20 % u otra oferta. El distintivo muestra el porcentaje de la oferta aplicada sobre el PVP sin descuento, y el catálogo considera el producto con descuento mientras esa oferta esté activa y reduzca su PVP. Si coinciden ofertas, se utiliza la más favorable, sin acumular porcentajes; las ofertas del mismo producto o categoría se actualizan, en vez de duplicarse.

El historial del menor precio de los 30 días anteriores se conserva y muestra de forma independiente cuando difiere del PVP base. No recalcula el porcentaje configurado, no cambia el importe y no elimina el artículo de los filtros, la selección de inicio, el aviso o los correos. La comparación del distintivo indica expresamente «sobre PVP»; el PVP sin descuento no se presenta como si fuera necesariamente ese mínimo histórico. Referencia normativa sobre el precio anterior: [artículo 20 de la Ley 7/1996](https://www.boe.es/buscar/act.php?id=BOE-A-1996-1072).

En familias con tallas, el catálogo muestra «Desde» y el menor precio disponible; cada talla conserva su precio y su oferta. Si solo algunas tallas tienen oferta, se indica «Tallas con descuento». Los correos y el aviso de un producto concreto enlazan una talla con descuento cuando corresponde.

## Mostrar ofertas y configurar el aviso

- En «Las ofertas de tu escaparate», pulsa «Elegir productos». El selector muestra fotos, precios, búsqueda y categorías; conserva lo elegido al cambiar de página o filtro. Pulsa las tarjetas para seleccionar hasta 8 artículos, sin duplicados, y «Usar esta selección». Cancelar descarta lo que has cambiado en el selector.
- Arrastra las tarjetas desde el asa para ordenar las ofertas del inicio, con ratón o pantalla táctil. También puedes usar «Subir», «Bajar» y «Quitar». Activa «Mostrar estas ofertas en el inicio» y pulsa «Guardar presentación» para publicar la selección y su orden, sin recargar la página. La sección solo aparece si está activada y queda algún producto disponible con descuento.
- Para el aviso emergente puedes elegir un producto o el anuncio general con enlace a todas las ofertas. Está desactivado inicialmente, aparece a los 10 segundos por defecto (configurable entre 5 y 60) y se muestra una vez cada 24 horas en el mismo navegador, incluso si se cierra y vuelve a abrir.
- Elige el producto del aviso con el mismo selector visual o utiliza «Usar anuncio general». «Ver cómo quedará» abre una vista previa de tu selección todavía sin guardar: no publica cambios ni consume las 24 horas. El aviso muestra una imagen grande y el porcentaje; el anuncio general presenta hasta tres ofertas y señala el descuento máximo con «Hasta».
- En «Imágenes del anuncio general», puedes elegir hasta 3 productos con descuento con el selector visual. Se respeta el orden de selección y se guarda con «Guardar presentación». La selección es independiente de las ofertas del inicio y del producto específico del aviso. «Volver a automáticas» recupera el comportamiento original. Si un producto deja de tener descuento se omite; si ninguno de los elegidos sigue disponible, se recurre a imágenes automáticas. La vista previa también utiliza la selección pendiente de guardar.
- El aviso solo aparece en inicio, catálogo y fichas. Espera si hay un aviso de cookies, otro diálogo abierto o se está rellenando un campo; no aparece durante el pago. Se cierra con Escape, el botón de cierre o «Seguir explorando».
- El catálogo permite «Todos», «Con descuento» y «Sin descuento», junto a los filtros de precio, orden y categorías. La selección se conserva en la URL, la paginación AJAX y los botones Atrás/Adelante.

## Correos comerciales

1. Los usuarios aceptan las ofertas opcionalmente en el registro o desde **Mi cuenta → Preferencias de ofertas**. Las cuentas existentes no se suscriben automáticamente. Las cookies no equivalen a consentimiento comercial.
2. «Elegir productos del correo» abre el mismo selector con fotos. El administrador puede elegir hasta 8 productos o una selección automática de hasta 4 ofertas vigentes. Escribe el asunto y pulsa «Preparar vista previa»: todavía no se envía nada.
3. Revisa imágenes, precios, enlaces y número de suscriptores. Solo «Enviar a los suscriptores» crea la campaña.
4. El proceso en segundo plano realiza envíos individuales con el SMTP existente; no expone direcciones de otros clientes. Comprueba suscripción y precios antes de enviar. Si se retira una oferta, cambia su precio, se elimina el usuario o se da de baja, se omite lo que ya no corresponda.
5. «Historial de correos» permite consultar enviados, pendientes, omitidos e incidencias. Una confirmación repetida no duplica la campaña. Los fallos y los resultados inciertos no se reintentan automáticamente: SMTP no garantiza entrega exactamente una vez, y así se evita repetir envíos ambiguos.

Cada correo incluye un enlace de baja sin iniciar sesión. La baja requiere confirmar en la página para que los analizadores automáticos de enlaces del correo no cancelen suscripciones accidentalmente. Volver a suscribirse genera una nueva versión de consentimiento; un enlace antiguo no cancela esa nueva elección. La baja no afecta a mensajes de pedidos, facturas o recuperación de contraseña.

Los enlaces usan el dominio público configurado o, mientras se trabaja en privado, la dirección desde la que se prepara el correo. Las imágenes enlazan el servicio público de imágenes de la web: para que el destinatario las vea, esa dirección debe ser accesible para él. No hay píxeles de seguimiento.

## Datos y despliegue

Se crean automáticamente, en el mismo MySQL de la web, las tablas `promociones_config`, `promociones_precios`, `promociones_suscripciones`, `promociones_campanas` y `promociones_envios`. Inclúyelas en copias de seguridad y migraciones; no hay que modificar manualmente las tablas de productos ni MongoDB. El historial registra los precios ofrecidos por el catálogo desde la integración, sin inventar el período anterior.

El servidor necesita estar en ejecución para enviar las campañas pendientes; al volver a iniciarse continúa con las pendientes. Guarda las claves persistentes de ASP.NET Data Protection junto con la instalación para mantener válidos los enlaces de baja. Revisa el SMTP y la dirección pública al desplegar.

El carrito se recalcula antes de iniciar el pago. Cuando se crea un intento de Stripe se fija una firma de las líneas y el importe; cambios posteriores de oferta no cambian un pago ya iniciado. Se comprueban cuenta, intento, líneas, importe y moneda antes de confirmar el pedido. Los pedidos y facturas anteriores conservan los precios con los que se realizaron.

## Verificación

- `dotnet run --project Tests/PromotionChecks` verifica reglas, redondeo, solapamientos, referencias, tallas, firma del pago, baja protegida y HTML comercial, sin servicios externos.
- Comprobados catálogo y panel con AJAX, móvil, oscuro y alto contraste, selección de inicio, aviso y consentimiento de cookies, campañas simuladas, idempotencia, baja, precios de carritos de invitado/usuario y un pedido de prueba en bases aisladas. Sin SMTP ni cobros reales.
- Las políticas de privacidad y cookies explican el consentimiento comercial y el recordatorio de 24 horas del aviso.

## Acciones desde la ficha del producto

Si eres administrador, la ficha incluye «Oferta de este producto». Puedes cambiar el porcentaje directamente, sin poner nombre a una promoción ni preparar un correo. Con 0 se retira únicamente la oferta individual; las ofertas de categoría siguen aplicándose si corresponden.

«Mostrar en ofertas del inicio» añade y activa la sección. Si ya contiene 8 productos, un diálogo permite elegir cuál reemplazar, como en los destacados. También puedes quitarlo desde la misma ficha. Al guardar el descuento se actualizan los precios de la ficha sin recargar la página; la selección del inicio se guarda inmediatamente.

Los correos incluyen versiones HTML y de texto. «Enviado» significa que el servidor de correo aceptó el mensaje: la bandeja de destino o spam depende del proveedor receptor. No se reenvía una campaña automáticamente por estar en spam.
