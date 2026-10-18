# Apariencia de la web

El selector de la cabecera permite elegir Sistema, Claro, Oscuro y Alto contraste. En móvil aparece como un icono de luna y abre las mismas opciones. La presentación del catálogo incluye el mismo selector.

Sistema sigue la configuración del dispositivo, priorizando el alto contraste si está solicitado. Una elección manual prevalece sobre el sistema y se guarda en ese navegador. La selección se comparte entre páginas y pestañas del mismo sitio. Se respetan las preferencias anteriores del control de alto contraste; si el navegador bloquea el almacenamiento, el tema sigue funcionando durante la página actual.

El tema se aplica desde el encabezado, antes de dibujar el contenido. `wwwroot/css/theme.css` adapta las utilidades existentes de Tailwind para fondos, tarjetas, tablas, formularios, alertas, controles y estados de interacción. El diseño responsive de las vistas se conserva. Las fotos mantienen sus colores originales; en modo oscuro, las fichas mantienen un fondo blanco para la imagen.

`wwwroot/css/high-contrast.css` define el alto contraste sin depender del modo oscuro: fondo negro, texto blanco, acciones amarillas y foco de teclado cian. Los controles seleccionados, desactivados y con error se distinguen mediante bordes, formas y sus etiquetas. Los enlaces de texto están subrayados; el texto dentro de un botón hereda su color para seguir siendo legible al pasar el ratón o seleccionarlo. Las categorías y las miniaturas de la ficha comunican su selección con `aria-pressed`. No se aplican filtros a las fotos. Stripe incluye campos con borde blanco, foco cian y errores legibles en esta modalidad.

Stripe usa una apariencia acorde con el tema y la actualiza sin crear otro intento de pago ni reiniciar sus campos. No cambia la lógica de cobro.

Pruebas de preferencia y compatibilidad: `node Tests/ThemeChecks/theme.test.cjs`.
