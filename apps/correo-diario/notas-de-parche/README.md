# Notas de parche

## 0.4.1-beta.1 · 21/11/2026

- Corregida la comparación del tamaño del objeto de Outlook con el tamaño del
  archivo: pueden diferir por las propiedades del adjunto.
- Segunda lectura de cada adjunto y comparación de longitud y SHA-256 antes
  de verificar la exportación y permitir cualquier movimiento.
- El recibo registra tamaño informado, bytes reales y resultado de verificación.
- Distingue un fichero vacío de una respuesta sin contenido.
- Se conservan el intervalo Desde/Hasta y el borrado opcional desmarcado.
- 71 pruebas y flujo Windows con cinco PDF y cinco adjuntos, tamaños de almacén
  distintos del contenido, diez lecturas y cero movimientos. Servicio OWA
  sintético; pendiente confirmar la exportación con las cuentas reales.

## 0.4.0-beta.1 · 21/11/2026

- Corregida la petición GetAttachment: incluye BaseShape obligatorio para
  evitar el 400 del formato anterior. No se altera el identificador del adjunto.
- Campos Desde/Hasta, con los dos días completos incluidos, en web y clásico.
- Hoy selecciona el día actual en ambos campos; fechas iguales buscan un día.
- Fecha y hora en la tabla y ordenación cronológica entre meses.
- Cambiar fechas limpia la lista anterior y desmarca el borrado.
- 65 pruebas y flujo Windows con intervalo de dos días, cinco PDF reales de
  WebView2 y cinco adjuntos frente a un servicio HTTPS sintético que valida
  búsqueda y adjuntos. Se mantienen cero movimientos con borrado desmarcado.
- Pendiente confirmar la exportación con las cuentas reales del usuario.

## 0.3.1-beta.1 · 21/11/2026

- Corregido el formato del filtro por fecha que provocaba un 400 al buscar.
- FindItem pide el resumen de destinatarios; GetItem obtiene los datos completos.
- La búsqueda distingue fecha de envío y recepción y respeta el día local.
- Al empezar una búsqueda se limpia la selección anterior, incluso si falla.
- Los errores HTTP identifican la operación que ha fallado.
- 59 pruebas y flujo en Windows con Edge, servicio OWA sintético, búsqueda
  paginada, cuatro PDF reales de WebView2 y cuatro adjuntos. Cero movimientos
  con el borrado desmarcado. La exportación con la cuenta real sigue pendiente.

## 0.3.0-beta.1 · 21/11/2026

- Corregida la causa de autorización detectada tras el error 401: se reutilizan
  las cabeceras y el destino de solicitudes correctas de la pestaña elegida.
- Credenciales de sesión solo en memoria; se descartan al desconectar.
- Renovación mediante Actualizar en Outlook, conservando la pestaña seleccionada.
- Una lectura puede repetirse una vez con autorización renovada. Los movimientos
  nunca se reintentan automáticamente.
- Mejor contraste de botones deshabilitados y casilla; subtítulo sin recorte.
- 50 pruebas de lógica y regresión con Edge real frente a un servicio HTTPS
  sintético que exige autorización. Las cuentas y la impresión reales siguen
  pendientes de comprobar.

## 0.2.0-beta.1 · 21/11/2026

- Retirado el inicio de sesión dentro del programa.
- Conexión con la pestaña de Outlook ya abierta en Edge o Chrome, con permiso
  concedido por el propio navegador y sin instalar una extensión.
- Selector de pestañas y guía para habilitar la conexión. La cuenta personal y
  la laboral se eligen por su pestaña abierta.
- Comprobación del origen y del identificador de la pestaña antes de operar.
- Desconexión del programa sin cerrar la cuenta ni el navegador.
- Se conserva el borrado opcional tras terminar y verificar toda la exportación.

Esta beta necesita que el navegador permita esa conexión. Las pruebas del
transporte simulado y del arranque se distinguen del permiso y la conexión real
con las cuentas del usuario. WebView2 se mantiene solo para generar PDF.

## 0.1.0-beta.1 · 21/11/2026

Primera publicación de prueba de CorreoDiario.

- Exportación de mensajes por fecha y carpetas a PDF.
- Adjuntos originales separados, con nombres relacionados con cada correo.
- Conexión web y acceso al Outlook clásico del equipo.
- Casilla de movimiento a Eliminados desmarcada por defecto.
- Apertura y cotejo de texto del PDF, tamaños, SHA-256 y revisión del origen
  antes de empezar cualquier movimiento.
- Registro de resultados, movimientos confirmados y movimiento sin confirmar;
  sin reintentos automáticos de movimiento ni eliminación definitiva.

Se mantiene como beta: quedan pendientes las pruebas reales de autenticación,
impresión y cuentas personales/laborales. La validación publicada diferencia
esas limitaciones de las pruebas automáticas y del arranque comprobado.
