# CorreoDiario 0.2 beta

Aplicación portátil de Windows para exportar correos del día a PDF y conservar
sus adjuntos originales. Incluye acceso web a Outlook y acceso al perfil del
Outlook clásico instalado. Consulta [LEEME.txt](LEEME.txt) para el uso y los
límites de la beta.

## Estado

- Compilación para Windows x64, autocontenida y en un ejecutable.
- 35 comprobaciones de lógica y conexión con fuentes simuladas; consultar el informe de la publicación para Windows.
- Pendientes: prueba real de la interfaz Windows, WebView2, autenticación de
  Outlook.com/Microsoft 365, correo laboral y automatización del Outlook clásico.
- La comprobación de renderizado Chromium incluida no pudo ejecutarse en este
  entorno por restricciones del proceso del navegador; no se contabiliza como
  una prueba superada. Las comprobaciones del lector PDF sí se ejecutaron.

## Compilación

Requiere SDK .NET 10 y acceso a NuGet. Puede compilarse para Windows desde Linux.

```sh
dotnet run --project tests/CorreoDiario.SafetyTests/CorreoDiario.SafetyTests.csproj -c Release
dotnet publish src/CorreoDiario.Windows/CorreoDiario.Windows.csproj -c Release -o dist
```

Se distribuye `dist/CorreoDiario.exe`. Las referencias administradas, el cargador
nativo de WebView2 y .NET se incluyen en ese EXE. El WebView2 Runtime es un
componente externo del equipo; no se incluye un instalador ni se instala al abrir.

## Verificación y movimiento

El motor exporta todos los correos seleccionados, abre los PDF, verifica la
escritura por tamaño y SHA-256 y vuelve a consultar los mensajes. Mantiene
abiertos los archivos con permisos de lectura durante la fase de movimiento.
El renderizador coteja las palabras del texto visible con el texto extraído del
PDF. Esta comprobación no certifica identidad visual, puntuación ni recursos
externos que Outlook no haya incluido como adjuntos.

La opción de movimiento es `false` por defecto. El motor solo puede solicitar
movimiento a Eliminados; no hay operación de eliminación definitiva. Persiste
el movimiento pendiente antes de llamar al servidor y cada movimiento confirmado
después de su respuesta. No pretende una transacción atómica entre varios correos.

## Conexiones

`OwaSource` usa JSON de Outlook Web Access dentro de la pestaña HTTPS elegida por el usuario,
sin registro de una aplicación Entra y sin extraer tokens bearer del navegador.
El navegador aporta su propia sesión y, cuando está disponible, el encabezado CSRF
X-OWA-CANARY. Esta interfaz interna no tiene garantía de estabilidad y puede no
estar disponible para determinados servidores/cuentas; se informa del fallo.
No se omiten controles de acceso, MFA, certificados ni políticas del servidor.

`OutlookClassicSource` usa el modelo de objetos de Outlook a través del perfil
local configurado. No instala un complemento, no modifica la configuración y
no es un acceso al nuevo Outlook. Los avisos de seguridad de Outlook, si
aparecen, se mantienen.

## Organización

- `src/CorreoDiario.Core`: contratos, conexión OWA independiente de la interfaz,
  exportación, comprobación de PDF y recibos de movimientos.
- `src/CorreoDiario.Windows`: interfaz, sesiones WebView2, impresión y Outlook clásico.
- `tests/CorreoDiario.SafetyTests`: pruebas de fallos, ausencia de borrado, cambios
  de origen, integridad, colisiones, paginación y cambio de hora.
- `tests/renderer-check.cjs`: comprobación adicional del saneado HTML y del PDF
  mediante Playwright y Chromium. Requiere configurar el entorno de desarrollo.

## Referencias de desarrollo

- [Publicación en un archivo .NET](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview).
- [Impresión desde WebView2](https://learn.microsoft.com/microsoft-edge/webview2/how-to/print).
- [Comparativa de Outlook clásico y nuevo](https://support.microsoft.com/outlook/getstarted/feature-comparison-between-new-outlook-and-classic-outlook).
- [Filtros de fecha en Outlook](https://learn.microsoft.com/office/vba/outlook/how-to/search-and-filter/filtering-items-using-a-date-time-comparison).
- [PdfPig](https://github.com/UglyToad/PdfPig).
- [cli-365, referencia pública del protocolo OWA](https://github.com/lc0rp/cli-365).

Las licencias de las dependencias redistribuidas se incluyen en `licencias`.

## Conexión al navegador abierto

La ventana de autenticación integrada se ha retirado. `BrowserSocket` conecta
solo con el WebSocket de bucle local publicado en `DevToolsActivePort` tras
habilitar el permiso en Edge o Chrome. No arranca otro perfil, no utiliza Node,
no lee bases de cookies o contraseñas y no cierra el navegador al desconectar.
`BrowserTabSession` fija la pestaña y verifica el origen antes de cada operación,
con una segunda comprobación dentro de la página. `OwaPageTransport` conserva
las llamadas de correo en ese contexto. No pide ni recibe contraseñas.
El permiso del navegador es más amplio que la pestaña: el producto limita las
operaciones, pero no presenta el permiso técnico como limitado por el navegador.

Prueba adicional del transporte local (requiere sockets, se ejecuta en Windows CI):

```sh
dotnet run --project tests/CorreoDiario.SafetyTests/CorreoDiario.SafetyTests.csproj -c Release -- --verify-browser-bridge
```

Referencias: [Edge, conexión a la instancia abierta](https://learn.microsoft.com/microsoft-edge/web-platform/devtools-mcp-server)
y [Chrome, conexión con permiso del usuario](https://developer.chrome.com/blog/chrome-devtools-mcp-debug-your-browser-session).
