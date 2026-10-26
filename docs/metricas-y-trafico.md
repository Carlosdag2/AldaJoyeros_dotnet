# Métricas y tráfico en vivo

El panel de administración permite elegir 7, 30 o 90 días naturales, incluyendo hoy. Las fechas se muestran en horario de Madrid. Las gráficas, rankings e importes del periodo se calculan con el mismo intervalo. Los importes corresponden a líneas de pedidos no cancelados, no a cobros confirmados. Se conservan por separado el resumen de hoy/7/30 días y los totales históricos.

El tráfico solo se mide con el consentimiento de estadísticas. El middleware registra cargas HTML correctas; excluye redirecciones, errores, JSON, recursos, rutas privadas y robots reconocibles. No guarda consultas de búsqueda, IP, referencias de producto ni números de pedido: únicamente identificadores aleatorios y secciones de la web.

Una carga HTML correcta confirma inicialmente la actividad, incluso si el navegador aún no ha enviado el primer latido. Las páginas abiertas envían un latido cada 20 segundos en primer plano y cada 40 segundos en segundo plano. Cambiar a la ventana del administrador no cierra la visita. Al navegar o cerrar una pestaña se envía una señal de salida; si se pierde, la presencia vence a los 120 segundos sin actividad. Varias pestañas de un navegador cuentan como un visitante y se prioriza su sección visible más reciente. Incógnito, otro perfil u otro dispositivo con identificador distinto cuentan por separado.

Los latidos no incrementan las páginas vistas. Una secuencia por pestaña evita reactivarla mediante una señal antigua que llegue tarde. Retirar el consentimiento elimina presencia y visitas identificables y bloquea señales en tránsito.

El panel consulta únicamente el resumen de tráfico cada 5 segundos. Permite pausar y actualizar manualmente; deja de consultar cuando no está visible. Ante fallos conserva los últimos datos, muestra el aviso y reintenta con espera creciente. Si caduca el acceso, detiene las consultas y ofrece iniciar sesión. El histórico reciente indica el máximo de visitantes activos por minuto durante 15 minutos; los otros contadores cubren 30 minutos.

Los datos de tráfico siguen en memoria: se reinician con la aplicación y son locales a esa instancia. Las colecciones están acotadas a 10.000 señales de presencia, 10.000 cargas recientes y 15 minutos de agregados; no representan analítica histórica persistente ni usuarios únicos reales entre dispositivos.

Pruebas: `dotnet run --project Tests/TrafficChecks/TrafficChecks.csproj`. Utilizan un reloj simulado y peticiones HTTP locales sin bases de datos. Incluyen presencia, múltiples pestañas, cierre, expiración, consentimiento, señales retrasadas, clasificación de respuestas y periodos de informe.

Las tres gráficas incluyen cinco marcas numéricas en el eje, fechas y el valor exacto del punto seleccionado. Admiten ratón, un selector táctil y teclado. Las escalas de visitantes y pedidos usan números enteros, y los importes se muestran en euros. La tabla diaria del informe sigue disponible.
