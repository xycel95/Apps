# Cómo funciona y guía

1. Descomprime el ZIP y abre `CorreoDiario-Edge.exe`. Abre Outlook en la pestaña visible de Edge y marca los correos en la lista.
2. En CorreoDiario pulsa **Detectar Edge**, elige la ventana y pulsa **Leer selección**. Revisa las casillas de los correos que quieres exportar.
3. Comprueba que la carpeta de descargas indicada es la que usa Edge. Si Edge pregunta dónde guardar un original, elige esa carpeta.
4. Elige la carpeta de destino y pulsa **Exportar selección**. Mantén la ventana de Outlook en primer plano mientras la app lee los mensajes.
5. Pulsa **Abrir resultado**. Cada carpeta de mensaje contiene `Correo.pdf`, `Original.eml` u `Original.msg` y los adjuntos. Si ya hay un `Correo.pdf` como adjunto, se guarda como `Correo (2).pdf`.

### Exportar archivos ya descargados

Pulsa **Archivos originales**, selecciona uno o varios EML o MSG y elige el destino. Esta opción también sirve cuando el menú de tu Outlook no ofrece la descarga a través de la app.

### Recuperar una conversación

Marca **Buscar también el origen y las respuestas en Outlook**. Si tu Outlook lo permite, selecciona **Todas las carpetas** en el buscador para incluir Enviados o Archivo. La app utiliza las cabeceras y el índice de conversación para relacionar mensajes. Cada respuesta recuperada conserva su carpeta. En `Hilos` aparece además el PDF conjunto cuando hay varios mensajes relacionados.

`Resumen.txt` indica qué se recuperó y qué falta. `Origen.json` recoge las cabeceras disponibles, la procedencia y las huellas de los archivos. Las imágenes externas se señalan en el PDF. Edge puede conservar las descargas originales y Outlook puede marcar los mensajes abiertos como leídos.

[Manual incluido en el ZIP](../Codigo/Edge/LEEME.txt) · [Descargar](../descargas/README.md)

### Comprobaciones de la beta

El ejecutable distribuido abrió su ventana en Windows y superó la comprobación de arranque. El motor pasó 37 comprobaciones con mensajes sintéticos EML y MSG. La captura desde un buzón real de Outlook sigue pendiente de prueba.

[Informe de Windows](VALIDACION-WINDOWS.json) · [Comprobación de las descargas](VALIDACION-PUBLICACION.json)
