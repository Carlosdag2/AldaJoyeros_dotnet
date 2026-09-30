# Munreco en el panel de administración

Acceso: `/AdminMunreco`, desde la tarjeta «Catálogo de Munreco» del panel principal. Requiere rol ADMIN. Todos los formularios de cambios tienen protección antifalsificación; las sesiones y los informes no se sirven como archivos públicos.

## Uso

- **Comprobar cambios** consulta el catálogo B2B y guarda las diferencias sin modificar productos ni imágenes.
- **Actualizar ahora** consulta nuevamente el proveedor y aplica la sincronización validada. Añade novedades, oculta agotados/retirados y reactiva reposiciones automáticas. Conserva los precios y decisiones manuales; los productos originales mantienen su visibilidad propia.
- **Programación mensual** permite activar/desactivar y elegir día (1–28) y hora de Madrid. Se guarda en MySQL. Inicialmente está desactivada para evitar duplicar la tarea anterior de Codex.
- **Conectar cuenta** permite renovar el acceso con las credenciales B2B. Se envían al proveedor y no se guardan. La sesión se protege con ASP.NET Data Protection. Si Munreco exige una verificación interactiva adicional, el formulario mostrará que no pudo validar el acceso.
- El historial muestra las últimas 30 tareas, su resultado y los informes descargables. El estado activo se actualiza automáticamente.

El trabajo continúa aunque el administrador cierre la página. La aplicación tiene que estar en ejecución. Si estaba apagada en la fecha programada, al arrancar encola una única revisión pendiente y calcula la siguiente fecha. Una tarea interrumpida queda marcada; se puede volver a solicitar. Los datos ya importados se reconocen por referencia y hash.

## Organización y despliegue

`Services/Munreco` gestiona la cola, programación y acceso. `Tools/MunrecoSync` contiene el importador independiente. Compilar o publicar `AldaJoyeros.csproj` incluye el ejecutable y sus dependencias en `MunrecoRunner`. El servidor necesita el runtime .NET 10 y permisos para iniciar ese proceso.

Las tablas `munreco_jobs` y `munreco_schedule` se crean automáticamente; el usuario MySQL necesita permiso para crearlas. Una restricción única impide encolar dos tareas simultáneas y un bloqueo de MySQL coordina varias instancias de la web. El importador conserva también sus bloqueos contra ejecuciones externas.

Configuración opcional, mediante variables de entorno o configuración privada:

```json
{
  "Munreco": {
    "WorkerEnabled": true,
    "DataDirectory": "App_Data/Munreco",
    "DotnetPath": "dotnet"
  },
  "DataProtection": {
    "KeysPath": "App_Data/DataProtectionKeys"
  }
}
```

`ImageStore` y `RunnerPath` permiten cambiar rutas cuando sea necesario. No deben configurarse desde parámetros del navegador. Se carga `appsettings.Local.json` cuando existe; está ignorado por Git y excluido de la publicación. La configuración de entorno tiene prioridad. Las conexiones se leen de la configuración habitual de la web y se pasan al importador en el entorno del proceso, sin escribir contraseñas de bases de datos en argumentos ni informes. La publicación también excluye el `appsettings.json` local: configurar en el alojamiento las conexiones, JWT, correo y pagos mediante sus valores privados de producción.

`App_Data/Munreco` conserva la sesión cifrada, imágenes de trabajo e informes. `App_Data/DataProtectionKeys` conserva las claves necesarias para abrir esa sesión. Ambas carpetas están excluidas de Git y de los archivos publicados: trasladarlas mediante un canal privado o conectar de nuevo la cuenta. Mantener almacenamiento persistente y permisos de acceso limitados al servicio. Las claves deben protegerse también en el alojamiento elegido. El endpoint de conexión B2B exige HTTPS fuera de desarrollo.

La caché local de imágenes ya se copió al proyecto; la ejecución desde el panel no depende de la carpeta de trabajo de Codex. Al desplegar, copiar esta caché evita descargar todas las imágenes de nuevo. Antes de activar la programación mensual del servidor, desactivar la antigua tarea de Codex para no duplicar calendarios.

## Comprobación

```powershell
dotnet build AldaJoyeros.csproj -c Release
dotnet Tools/MunrecoSync/bin/Release/net10.0/MunrecoSync.dll --self-test
```

`Tests/MunrecoPanelChecks` comprueba horarios/cambio de hora, cola persistente, concurrencia, recuperación, permisos ADMIN y antifalsificación. Crea y elimina únicamente una base temporal con nombre aleatorio. Las pruebas del navegador esperan una instancia local en `http://127.0.0.1:5089`; `--preview` solicita además una comparación real sin cambiar el catálogo. No incluye una opción para aplicar cambios reales durante las pruebas.

Validación local del 30/09/2026: compilación y publicación local correctas; 19 comprobaciones del panel y 17 del importador superadas. Se comprobó una comparación completa de 2.696 referencias, el historial, la descarga del informe, la eliminación de sesiones temporales y la vista móvil. Una actualización manual solicitada desde el panel terminó después sin errores, conservando los 2.716 productos previos y verificando 7.610 enlaces de imágenes. No se encontraron diferencias nuevas. Los informes de esa actualización están en `App_Data/Munreco/runs/c0c9f1c65bf34aa6aa98619f77a423b7`.

La compilación conserva cinco avisos previos de dependencias vulnerables (AutoMapper, MailKit, MimeKit, SharpCompress y Snappier); esta integración no actualiza esas dependencias de la tienda.
