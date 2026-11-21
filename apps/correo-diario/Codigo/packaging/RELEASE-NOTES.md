# CorreoDiario 0.3 beta

Corrige la conexión que detectaba la pestaña de Outlook pero devolvía 401 al
leer las carpetas. Reutiliza la autorización de solicitudes correctas de esa
pestaña, solo en memoria. Mantiene el navegador habitual, sin inicio de sesión
dentro del programa ni extensión.

Si pide renovar la conexión, pulsa **Actualizar** en Outlook y vuelve a
**Usar esta pestaña**. Corregido también el contraste de los textos y el subtítulo.

El borrado sigue desmarcado y solo mueve a Eliminados tras verificar toda la
exportación. No se reintentan movimientos ni se vacía Eliminados.

Validación: 50 pruebas de lógica, transporte WebSocket, arranque nativo Windows y
conexión a Edge real con servicio HTTPS sintético que reproduce 401 y exige
cabeceras de autorización. Pendientes las cuentas reales y la impresión del usuario.

Descarga el EXE o el ZIP. Windows x64; .NET incluido; necesita WebView2 Runtime
para componer los PDF. Consulta SHA256SUMS.txt para comprobar integridad.
