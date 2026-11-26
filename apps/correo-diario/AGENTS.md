# Trabajar en CorreoDiario

## Edición para Edge 0.7 y posteriores

El flujo por carpetas usa un registro propio delegado Mail.Read, el navegador del sistema y la cuenta elegida por el usuario. Respetar las decisiones del directorio y conservar en memoria las credenciales de sesión. Las preferencias locales contienen solo identificadores de configuración, carpetas y destino. El lector de Graph solo emite GET. Confirmar la copia de PDF y adjuntos antes de considerarla verificada, reanudar por identidad del mensaje y comprobar la privacidad de los diagnósticos. Mantener la compatibilidad con la entrada local descrita a continuación.

## Edición para Edge 0.6

Código en `Codigo/Edge`. La detección y los contadores de Edge son de solo lectura y opcionales. La entrada de mensajes completos se hace mediante una entrega explícita de Windows o la selección de archivos existentes. La exportación trabaja en segundo plano. Mantén la entrega en memoria, la integridad de adjuntos y la agrupación de los mensajes recibidos. El programa no descarga ni escribe copias del mensaje fuente y no controla la entrada ni el foco de otras ventanas. Comprueba el motor con `dotnet run -c Release --project Checks/Checks.csproj` y el flujo nativo con WindowsChecks. Las reglas de depuración remota, Outlook clásico y movimiento a Eliminados que siguen corresponden a la línea histórica 0.4.x.

Aplicar las instrucciones del AGENTS.md de la raíz. Responder en español.

- Rama `correo-diario`, aplicación en `apps/correo-diario/`, código en `Codigo/`.
- Leer CONTEXTO.md, app.json, notas y revisión remota antes de editar.
- Mantener los cuatro apartados públicos: descripción, guía, descarga y notas.
- Conservar versiones anteriores, licencias y comprobaciones reales.
- El movimiento a Eliminados es opcional y viene desmarcado. No comenzar
  movimientos antes de completar y verificar toda la exportación seleccionada.
- No introducir eliminación definitiva, vaciado de papelera ni reintentos
  automáticos de operaciones cuyo resultado no se ha confirmado.
- Preservar contenido, nombres y relación de correos y adjuntos; evitar
  sobrescrituras. Distinguir la comprobación de bytes/texto de una réplica visual.
- No subir correo real, sesiones, cookies, contraseñas, tokens ni datos de cuentas.
- No prometer compatibilidad universal de Outlook; separar web, clásico y nuevo.
- Distinguir pruebas simuladas, arranque nativo, impresión y acceso a cuentas reales.
- Los binarios se publican como assets de una release; no añadirlos al historial.
- Se puede usar un flujo temporal para compilar, comprobar y publicar una versión;
  retirar el disparador al terminar. No crear programaciones ni actualizadores.
- `main` solo recibe la entrada del catálogo. No fusionar automáticamente la rama
  ni modificar otras aplicaciones o el repositorio privado de la web.

- Requisito vigente: el usuario rechaza iniciar sesión dentro del programa.
  Para la detección visual utilizar su pestaña ya abierta. Para el modo por carpetas, usar el navegador corporativo y un registro autorizado. No reintroducir
  autenticación integrada ni crear un perfil de navegador distinto.
- No cambiar la opción de depuración/conexión ni aceptar sus permisos por el
  usuario. No copiar bases de cookies o contraseñas; no presentar el permiso
  del navegador como limitado técnicamente a una sola pestaña.
- La desconexión cierra solo el enlace de CorreoDiario, no la cuenta ni el navegador.
