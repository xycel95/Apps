# Descripción del programa

CorreoDiario 0.4.3 beta trabaja con la sesión de Outlook que ya tienes abierta
**en tu navegador habitual**. Permite buscar las pestañas de correo, elegir
una, seleccionar carpetas y un intervalo de fechas, y exportar los mensajes a PDF con sus
adjuntos originales separados. Conserva el modo alternativo de Outlook clásico. Los campos **Desde** y **Hasta**
incluyen ambos días. Si son iguales, busca solo ese día. La lista muestra fecha
y hora para distinguir mensajes de varios días.

El acceso web se realiza con el permiso que solicita Edge o Chrome. No hay
una pantalla de inicio de sesión dentro del programa. No se instala una extensión,
no se lanza un perfil de navegador nuevo y no se copian bases de contraseñas
ni cookies de tu perfil. Reutiliza las cabeceras de autorización de solicitudes
correctas de la pestaña elegida; se mantienen solo en memoria y se descartan
al desconectar. Si es necesario, pide pulsar Actualizar en Outlook.

## Conexión con tu navegador

La opción de conexión debe estar habilitada en `edge://inspect/#remote-debugging`
o `chrome://inspect/#remote-debugging`, según el navegador. La aplicación incluye
un botón **Cómo conectar** con los pasos. El navegador muestra un aviso para
aceptar o rechazar la conexión.

Ese permiso técnico da acceso a la sesión del navegador. CorreoDiario limita
sus operaciones a la pestaña que selecciones y comprueba su origen antes de
actuar. Las políticas del equipo pueden impedir habilitar esta opción.

## Requisitos y límites

Windows de 64 bits, Edge o Chrome con esta función de conexión y perfil estándar,
y Microsoft Edge WebView2 Runtime para componer los PDF localmente. WebView2
se utiliza para impresión, no para iniciar sesión en el correo.

La compatibilidad del servicio interno de Outlook sigue dependiendo del servidor.
La conexión y la exportación con las cuentas del usuario no están certificadas
por las pruebas simuladas. Las imágenes externas no se descargan y la composición
PDF puede variar respecto al cuadro de impresión de Outlook.

El PDF adapta las tablas al ancho de página y retira alturas fijas o recortes
que pueden esconder texto al imprimir. El cotejo reconoce palabras partidas
entre líneas o páginas si conserva todas sus letras y cifras. Si detecta una
omisión, detiene la operación y conserva los correos.

El borrado es opcional y mueve a Eliminados después de verificar toda la
exportación. No vacía esa carpeta ni garantiza liberar cuota inmediatamente.

## Comprobación de adjuntos

El tamaño que informa Outlook puede incluir propiedades del objeto adjunto y
ser mayor que el archivo. La aplicación distingue ese dato del tamaño real de
los bytes descargados. Vuelve a leer cada adjunto y compara longitud y SHA-256
antes de considerar verificada la exportación; también comprueba la escritura
en disco. Esto implica dos lecturas por adjunto.

## Carpeta de destino

Todos los PDF y adjuntos se guardan directamente en la carpeta que elijas.
No se crea una subcarpeta por ejecución ni por carpeta del buzón. Los nombres
repetidos reciben un sufijo numérico y se conserva la relación entre cada PDF
y sus adjuntos. Cada ejecución tiene un registro Comprobacion_….json propio.
