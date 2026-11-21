# Notas de parche

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
