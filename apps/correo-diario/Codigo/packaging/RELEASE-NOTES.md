# CorreoDiario 0.4.1 beta

Corrige el aviso de tamaño de adjunto: Outlook puede informar el tamaño del
objeto con sus propiedades, que no equivale al tamaño del archivo descargado.

La copia se comprueba con una segunda lectura de cada adjunto y comparación
de longitud y SHA-256, además de la revisión del correo y de los bytes en disco.
Si falla, no comienza el movimiento de ningún correo. El recibo distingue el
tamaño informado por Outlook y el tamaño real guardado.

Conserva Desde/Hasta, ambos días incluidos, y borrado opcional desmarcado.
No reintenta movimientos ni vacía Eliminados.

Validación: 71 pruebas, Edge real y servicio HTTPS sintético con tamaño de
almacén 344 frente a contenido de 8 bytes; cinco PDF de WebView2, cinco
adjuntos, diez lecturas y cero movimientos. Pendiente la exportación con la
cuenta real y el perfil de Outlook clásico del usuario.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
Consulta SHA256SUMS.txt para verificar la descarga.
