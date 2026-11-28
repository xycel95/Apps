# Cómo funciona y guía

## Exportar una selección desde Edge

1. En CorreoDiario, elige la carpeta de destino y pulsa **Abrir Outlook web**. Si tu organización lo solicita, identifica tu cuenta en esa ventana de Microsoft Edge.
2. En esa ventana de Edge, abre la carpeta y marca los mensajes que quieras. Para un solo correo también puedes abrirlo en el panel de lectura. Vuelve a CorreoDiario y pulsa **Exportar selección web**.
3. Deja abierta la ventana de exportación. Puedes seguir trabajando en otra pestaña o aplicación mientras CorreoDiario procesa los mensajes en segundo plano. Al terminar, pulsa **Abrir resultado**.

Cada correo verificado tiene una carpeta directamente en el destino elegido, por ejemplo `2026-11-28_09.35_remitente@example.test_Solicitud`. El nombre usa el día y la hora del correo, la dirección del remitente y el asunto. Dentro están `Correo.pdf` y los adjuntos con sus nombres. Si dos nombres coinciden, se añade un número entre paréntesis para conservar ambos archivos o carpetas.

Puedes marcar más filas de las que caben en pantalla. CorreoDiario coteja la selección con el contador de Outlook, abre cada correo en su propia ventana y comprueba la correspondencia entre el PDF, los adjuntos y el mensaje. Usa la opción **Descargar** del propio adjunto. Outlook puede marcar el correo como leído al abrirlo según la configuración del buzón.

En una conversación, la aplicación identifica los mensajes por separado. Un correo anterior citado en el cuerpo queda dentro del PDF del mensaje que lo cita. Si Outlook solo muestra el nombre del remitente, CorreoDiario intenta obtener su dirección de la ficha del remitente o de los detalles del mensaje. Un correo cuya identidad, fecha, contenido o adjuntos no se puedan verificar queda en **Ver pendientes**. La ventana principal indica la hora de finalización y las cantidades. Consulta los pendientes del último lote antes de cerrar la aplicación.

Si Outlook no expone todas las filas marcadas, la aplicación puede usar una vista de borrador con los mensajes adjuntos. El diagnóstico indica cuántos elementos ha entregado Outlook. Puedes volver a la selección desde la pestaña de conversaciones al pie de Outlook.

**Mostrar Outlook web** recupera la ventana para otro lote. **Cerrar ventana web** termina esa sesión. La primera apertura puede descargar de Microsoft el controlador firmado correspondiente al Edge instalado. Para este modo basta con que el equipo permita el controlador y que puedas acceder al buzón en esa ventana.

**Copiar diagnóstico** recoge las fases, los contadores y los motivos técnicos. Los asuntos y direcciones solo se muestran localmente.

## Archivos locales

Para convertir mensajes EML o MSG que ya tengas en el ordenador, despliega **Archivos locales**, pulsa **Añadir archivos…** y después **Exportar archivos añadidos**. La salida tiene la misma estructura que la exportación desde Edge.

**Detectar Edge** y **Ver selección en Edge** permiten comprobar los contadores de una pestaña de Outlook. Para exportar, marca los correos en la ventana que abre **Abrir Outlook web**.

### Comprobaciones de la versión

En Windows se superaron 65 comprobaciones de lectura y archivo y 97 de escritorio. Se exportaron 106 mensajes ficticios mientras otra ventana conservaba el foco y el cursor. También se comprobaron el nombre de las carpetas, el contenido de los PDF y los adjuntos, las citas, los remitentes y la vista inicial centrada en el navegador. La comprobación en el buzón de trabajo queda pendiente.

[Informe de Windows](VALIDACION-WINDOWS.json) · [Comprobación de las descargas](VALIDACION-PUBLICACION.json)

Actualizar CorreoDiario

Pulsa Buscar actualizaciones en la ventana principal. Si hay una versión estable posterior, confirma la descarga y el reinicio. La aplicación comprueba el tamaño y la huella SHA-256 publicados antes de reemplazar el ejecutable. La carpeta del programa debe permitir escritura. Las versiones hasta la 0.7.0 necesitan una primera descarga manual.
