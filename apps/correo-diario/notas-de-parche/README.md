# Notas de parche

## 0.5.1 beta · 26/11/2026

- Detección de ventanas nativas de Microsoft Edge y lectura de la barra en español e inglés mediante sus controles de accesibilidad. Si la barra no facilita el valor, se comprueba la dirección del documento superior cuando es inequívoca.
- La aplicación distingue una pestaña reconocida sin filas de una ventana cuya dirección no pudo confirmarse.
- Nuevo botón **Copiar diagnóstico**. Registra versiones, fase, ventanas, método de detección, contadores y tipo de error. Excluye títulos de pestaña, asuntos, URLs completas, direcciones de correo y rutas personales.

[Descargas de la versión](https://github.com/xycel95/Apps/releases/tag/correo-diario-v0.5.1-beta.1)

## 0.5.0 beta · 26/11/2026

- Selección de correos en la ventana visible de Outlook web en Edge y descarga de los originales mediante los controles de accesibilidad de Windows. También admite EML y MSG locales.
- Una carpeta por mensaje y por respuesta con `Correo.pdf`, el original y los adjuntos. Conserva los archivos cuando coinciden el nombre de un adjunto o la fecha, la hora y el asunto de dos correos.
- Búsqueda opcional de mensajes relacionados, identificación de antecesores pendientes y PDF conjunto de las conversaciones recuperadas.
- `Resumen.txt` y `Origen.json` recogen los resultados del lote. Esta edición no incorpora movimiento de correos a Eliminados.

[Descargas de la versión](https://github.com/xycel95/Apps/releases/tag/correo-diario-v0.5.0-beta.1)

## 0.4.8-beta.1 · 22/11/2026

- **Marcar pendientes** importa un registro desde 0.4.7 para reintentar solo
  esos mensajes. Exige copias verificadas, cero movimientos y coincidencia
  de todos sus IDs con la lista actual antes de cambiar las casillas.
- Tras un lote parcial verificado, solo quedan marcados los pendientes.
  El borrado queda desmarcado. No inicia otro intento automáticamente.
- **Ver error** reabre el aviso. **Copiar identificadores** es una acción separada
  para referencias CID fallidas y Content-ID de adjuntos, con aviso de posibles
  direcciones internas. Sin cuerpo, asuntos, nombres de archivo ni sesión.
  Copiar error y el registro siguen excluyéndolos; permanecen solo en memoria.
- 131 pruebas de lógica y comprobaciones Windows de selección, portapapeles
  y PDF reales. El motivo concreto de los CID pendientes requiere esa evidencia:
  esta versión no modifica la asociación ni omite imágenes.

## 0.4.7-beta.1 · 22/11/2026

- Aprovecha ContentId/ContentType devueltos por GetAttachment cuando faltan
  en el resumen del correo; conserva el snapshot original y vuelve a comprobar
  esos datos en la segunda lectura junto con longitud y SHA-256.
- Resolución de referencias CID con escapes, espacios y corchetes; decodifica
  una sola vez y conserva el signo más. No adivina una imagen por su nombre.
- Detecta identificadores ambiguos e imágenes cuyos bytes no se pueden decodificar.
- Si una imagen sigue sin poder recuperarse, marca ese correo pendiente y
  continúa con los demás. Verifica las copias guardadas y bloquea el borrado
  de todo el lote mientras quede algún pendiente.
- Copiar error incluye número de correo, pendientes y diagnóstico ImageFailure
  con recuentos/motivos, sin copiar referencias CID ni contenido del mensaje.
- 119 pruebas y flujo Windows con imágenes reales, dos copias verificadas,
  dos pendientes distintos y cero movimientos con la casilla marcada.

## 0.4.6-beta.1 · 22/11/2026

- Corregido el falso rechazo identificado con Copiar error: U+FE0F de
  presentación emoji aparecía como un carácter de texto perdido en el PDF.
- El cotejo excluye únicamente los selectores de presentación FE0E/FE0F.
  No cambia el HTML ni las imágenes y mantiene letras, cifras, acentos y
  otras marcas combinadas. No introduce tolerancias por número de caracteres.
- El registro cuenta los selectores excluidos en cada lado de la comparación.
- 100 pruebas, incluyendo casos que fallaban antes de esta corrección,
  negativos de letras/cifras/acentos y PDF reales de Windows con emoji.
- Conserva Copiar error/Copiar último error, fechas, destino directo,
  lectura doble de adjuntos y verificación global antes de mover a Eliminados.

## 0.4.5-beta.1 · 22/11/2026

- Botón **Copiar error** en el aviso, con informe seleccionable y desplazable.
- Botón **Copiar último error** en la ventana principal. Conserva el informe
  durante la sesión, aunque se cierre el aviso o se borre el archivo de registro.
- Copia versión, mensaje, progreso y diagnóstico de ambos cotejos. Añade códigos
  Unicode del primer carácter diferente, categoría y tipo de diferencia;
  no copia palabras ni fragmentos del cuerpo ni vuelca datos de sesión.
- 92 pruebas y comprobación del botón con el portapapeles real de Windows.
- No se cambia la aceptación del texto ni el borrado: sigue bloqueado ante
  una discrepancia. El fallo real de un carácter sigue pendiente de diagnóstico.

## 0.4.4-beta.1 · 21/11/2026

- La 0.4.3 seguía mostrando un rechazo en el caso real reportado. Esta versión
  añade una segunda composición cuando el PDF con formato no supera el cotejo.
- La alternativa usa texto sencillo e imágenes al final, y exige coincidencia
  completa de todas las letras y cifras, en orden y cantidad. Conserva la
  numeración de listas; el PDF indica que su formato se ha simplificado.
- El registro recoge modo, páginas y recuentos del cotejo. Si fallan ambos
  intentos, indica el número de correo y diagnóstico de cada intento, sin
  guardar fragmentos del cuerpo en esos diagnósticos.
- 88 pruebas y regresión Windows del rechazo que todavía podía producir 0.4.3:
  varias páginas, listas numeradas, imagen y segunda lectura del adjunto.
- Se mantienen destino directo, fechas y verificación global antes de mover.
  El caso concreto del usuario sigue pendiente de comprobar con esta versión.

## 0.4.3-beta.1 · 21/11/2026

- Corregido un falso rechazo del cotejo cuando el PDF parte palabras o
  direcciones entre líneas o páginas. Solo se admite si todas las letras
  y cifras coinciden en orden y cantidad en el documento completo.
- Tablas adaptadas al ancho de página; retiradas alturas y recortes del HTML
  que pueden ocultar contenido al imprimir. Cotejo bajo el formato de impresión.
- 83 pruebas de lógica. Regresión de Windows con el renderizador real:
  palabras largas, tablas de 90 filas, varias páginas, Unicode e imágenes CID.
- Las palabras omitidas, cifras cambiadas y contenido truncado siguen fallando.
- Se mantienen destino directo, Desde/Hasta, segunda lectura de adjuntos y
  borrado opcional desmarcado. No se han usado correos reales en estas pruebas.

## 0.4.2-beta.1 · 21/11/2026

- PDF y adjuntos directamente en la carpeta elegida, todos juntos.
- Retiradas las subcarpetas creadas por cada ejecución.
- Nombres repetidos con sufijo numérico común para el PDF y sus adjuntos;
  se conservan archivos existentes y exportaciones parciales.
- Registro de comprobación independiente por ejecución dentro del destino.
- 75 pruebas, incluidas ejecuciones sucesivas/simultáneas, y flujo Windows
  con cinco PDF y cinco adjuntos en un único destino sin subcarpetas.
- Se mantienen Desde/Hasta, verificación de contenido y borrado desmarcado.

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
