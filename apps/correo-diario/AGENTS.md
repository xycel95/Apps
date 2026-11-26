# Trabajar en CorreoDiario

## Edición para Edge desde la 0.5.0

El código vigente para esta edición está en `Codigo/Edge`. Conserva la descarga ofrecida por Outlook, la importación EML/MSG, una carpeta con los adjuntos por mensaje y la recuperación de hilos por cabeceras. Las pruebas sintéticas se ejecutan con `dotnet run -c Release --project Checks/Checks.csproj` desde esa carpeta. Mantén independientes la prueba en Windows con datos sintéticos y la validación con un buzón real. Las reglas de depuración remota, Outlook clásico y movimiento a Eliminados que siguen corresponden a la línea 0.4.x.

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
  Utilizar su pestaña de navegador ya abierta, con su permiso. No reintroducir
  autenticación integrada ni crear un perfil de navegador distinto.
- No cambiar la opción de depuración/conexión ni aceptar sus permisos por el
  usuario. No copiar bases de cookies o contraseñas; no presentar el permiso
  del navegador como limitado técnicamente a una sola pestaña.
- La desconexión cierra solo el enlace de CorreoDiario, no la cuenta ni el navegador.
