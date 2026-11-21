# Descripción del programa

CorreoDiario permite elegir una fecha, carpetas de correo y una carpeta de
destino. Guarda cada mensaje en un PDF y cada adjunto en su formato original.
Los nombres comparten fecha, hora, tipo RECIBIDO/ENVIADO y dirección para
identificar qué archivos pertenecen al mismo mensaje.

- Entrada, Enviados y otras carpetas accesibles; selección de mensajes.
- Inicio de sesión en una ventana propia para Outlook web.
- Acceso alternativo al perfil del Outlook clásico instalado en Windows.
- Escritura comprobada, apertura del PDF, cotejo de su texto y verificación de
  todos los archivos antes de empezar el movimiento opcional a Eliminados.
- Un ejecutable portátil con .NET incorporado. No instala extensiones ni complementos.

## Requisitos y límites

Windows de 64 bits y Microsoft Edge WebView2 Runtime en el equipo para la
ventana web y la impresión. Para utilizar el modo de Outlook clásico debe
estar instalado y tener una cuenta configurada. El nuevo Outlook se utiliza
por la conexión web, no por el modo clásico.

Esta es una beta. La conexión web usa una interfaz interna de Outlook que puede
cambiar o estar bloqueada por la configuración del servidor. No equivale a una
certificación de compatibilidad con todos los Outlook personales o laborales.

El PDF se compone a partir del HTML y las cabeceras; puede cambiar el aspecto
respecto al cuadro de impresión de Outlook. Las imágenes externas quedan
indicadas sin descargarse. Los adjuntos de nube o protegidos que no puedan
recuperarse impiden considerar completa la exportación.

El borrado mueve a Eliminados: **no vacía la papelera ni borra definitivamente**.
Esa carpeta puede seguir ocupando cuota del buzón. El detalle de lo comprobado
y lo pendiente está en [la guía](docs/README.md).
