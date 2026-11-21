# Descargar

**CorreoDiario 0.4.6 beta · Windows de 64 bits.**

- [Descargar el ejecutable](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.4.6-beta.1/CorreoDiario_0.4.6_beta.exe)
- [Descargar el ZIP con el ejecutable, guía y licencias](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.4.6-beta.1/CorreoDiario_0.4.6_beta.zip)
- [Descargar el código fuente](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.4.6-beta.1/CorreoDiario_0.4.6_beta_fuentes.zip)
- [Comprobar SHA-256](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.4.6-beta.1/SHA256SUMS.txt)
- [Ver la publicación](https://github.com/xycel95/Apps/releases/tag/correo-diario-v0.4.6-beta.1)

Si eliges el ZIP, descomprímelo y abre `CorreoDiario.exe`. Puedes mover ese
EXE a una carpeta compartida. Requiere Microsoft Edge WebView2 Runtime en
el equipo; .NET está incluido y no se instala una extensión en el navegador.

Esta versión usa la pestaña de Outlook que ya tienes abierta en Edge o Chrome.
Pulsa **Cómo conectar** para ver la opción del navegador que debes activar y
acepta su aviso de conexión. Después selecciona tu pestaña. Elige **Desde** y **Hasta**: ambos días se incluyen.
Para un solo día, pon la misma fecha en los dos campos. No hay un formulario
de inicio de sesión dentro de CorreoDiario ni se instala una extensión.
Si Outlook pide renovar la conexión, pulsa **Actualizar** en su pestaña y vuelve
a **Usar esta pestaña**. Las cabeceras de sesión se mantienen solo en memoria.
Si el PDF con formato no supera el cotejo, se crea una segunda composición
de texto sencillo con las imágenes al final. Se exige coincidencia completa
de letras y cifras en orden y cantidad; se indica el modo en el PDF y el registro. Los PDF y adjuntos se guardan directamente en la carpeta elegida. Si un nombre
coincide, se añade un número y se conservan los archivos anteriores.
Corrige el falso rechazo del texto por las marcas de presentación de emoji
FE0E/FE0F, sin relajar la comprobación de letras, cifras y acentos.
En los avisos puedes pulsar **Copiar error** y pegar el informe en el chat.
**Copiar último error** lo recupera mientras la aplicación siga abierta, aunque
cierres el aviso o borres el archivo de registro.
El modo clásico sigue disponible para perfiles configurados en Outlook clásico.

**Validación:** 100 pruebas automáticas, transporte WebSocket local, arranque
del EXE y flujo con Edge real frente a un servicio HTTPS sintético que exige
autorización y valida el contrato de búsqueda. Incluye búsquedas por intervalo de fechas y
carpeta, paginación, contrato de adjuntos, segunda lectura del contenido y PDF
reales de WebView2, composición alternativa con numeración e imágenes,
caracteres Unicode y tablas de varias páginas. Estas pruebas
automáticas utilizan un servicio de correo sintético. [Informe](../docs/VALIDACION-WINDOWS.json).

El borrado es opcional y mueve a Eliminados después de verificar toda la
exportación. No vacía esa carpeta; puede seguir ocupando cuota.
