# CorreoDiario 0.4 beta

Aplicación portátil de Windows para exportar correos de un intervalo a PDF y conservar
sus adjuntos originales. Incluye acceso web a Outlook y acceso al perfil del
Outlook clásico instalado. Consulta [LEEME.txt](LEEME.txt) para el uso y los
límites de la beta.

## Estado

- Compilación para Windows x64, autocontenida y en un ejecutable.
- 65 comprobaciones de lógica y conexión con fuentes simuladas; consultar el informe de la publicación para Windows.
- La publicación comprueba el arranque nativo y la conexión a Edge real con un
  servicio HTTPS sintético que exige autorización y valida el contrato de búsqueda.
- El flujo sintético incluye fecha/carpeta, paginación, lectura completa,
  generación de PDF mediante el renderizador WebView2 real y adjuntos separados.
- Pendientes: cuentas reales de Outlook.com/Microsoft 365, correo laboral,
  exportación en las cuentas del usuario y automatización del Outlook clásico.
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
sin registro de una aplicación Entra. Observa solicitudes correctas del marco
principal de esa pestaña y conserva en memoria únicamente las cabeceras de sesión
permitidas y el destino OWA utilizado. No persiste esas credenciales. El navegador
aporta sus cookies sin copiar sus bases de datos; las cabeceras Authorization y
X-OWA-CANARY se reutilizan cuando las emplea la propia pestaña. Esta interfaz interna no tiene garantía de estabilidad y puede no
estar disponible para determinados servidores/cuentas; se informa del fallo.
No se omiten controles de acceso, MFA, certificados ni políticas del servidor.

`OutlookClassicSource` usa el modelo de objetos de Outlook a través del perfil
local configurado. No instala un complemento, no modifica la configuración y
no es un acceso al nuevo Outlook. Los avisos de seguridad de Outlook, si
aparecen, se mantienen.

## Organización

- `src/CorreoDiario.Core`: contratos, conexión OWA independiente de la interfaz,
  exportación, comprobación de PDF y recibos de movimientos.
- `src/CorreoDiario.Windows`: interfaz, impresión WebView2 y Outlook clásico.
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

Prueba de regresión del 401 (Windows efímero de CI con Edge, sin cuenta real):

```sh
dotnet run --project tests/CorreoDiario.SafetyTests -c Release -- --verify-outlook-auth
```

El fixture crea un certificado local de corta duración, instala solo su CA pública
en el almacén de confianza del equipo efímero de CI y la elimina al terminar.
Solo se ejecuta dentro de GitHub Actions en Windows con permisos de su runner;
no cambia certificados en el equipo del usuario. Usa un
perfil de Edge desechable únicamente en la prueba. Comprueba un 401 inicial,
la observación de una solicitud nativa autorizada, la regeneración de
X-OWA-UrlPostData y la lectura de Entrada/Enviados. No desactiva TLS ni accede a
cuentas del usuario. El programa distribuido nunca crea ese perfil.

Una lectura rechazada por 401/440 puede esperar una autorización renovada y
repetirse una sola vez. MoveItem nunca se reintenta, ni siquiera tras un 401.

## Contrato de búsqueda y regresión del 400

`Restriction` contiene `Item`; la expresión `And` se coloca dentro de ese
miembro. El nombre JSON del valor constante es `Constant:#Exchange`, aunque el
tipo CLR del servidor se denomine ConstantValueType. En 0.3 se omitía la
envoltura y se enviaba `ConstantValue:#Exchange`.

`OwaSearchContract` es un lector de contrato independiente, basado en
DataContractJsonSerializer. Rechaza los dos formatos antiguos. El servicio
sintético exige ese contrato y reproduce 400 antes de comprobar la búsqueda
corregida en Entrada, Enviados y una carpeta adicional, con páginas de un
mensaje y otro correo fuera del día solicitado. FindItem pide DisplayTo,
porque devuelve resúmenes; los destinatarios completos se leen con GetItem.

La prueba Windows compila el mismo PdfRenderer.cs de la aplicación y recorre
el flujo con WebView2 real (cinco PDF y cinco adjuntos, cero movimientos):

```sh
dotnet run --project tests/CorreoDiario.WindowsChecks -c Release
```

Se ejecuta únicamente en el Windows efímero de CI. El servicio OWA es sintético;
no certifica la compatibilidad de todos los servidores de Microsoft ni accede
a correo real. La guía de FindItem de Microsoft describe sus resúmenes y las
propiedades completas obtenidas mediante GetItem:
https://learn.microsoft.com/exchange/client-developer/web-service-reference/finditem-operation

## Intervalos y descarga de adjuntos en 0.4

`MailDateRange` es común a OWA y Outlook clásico. Desde y Hasta son inclusivos;
se convierte la medianoche inicial y la del día posterior a Hasta por separado
a UTC para respetar cambios de hora. La comprobación local aplica el mismo
intervalo. Se rechaza Hasta anterior a Desde. Hoy pone las dos fechas actuales;
cambiar fechas limpia los resultados anteriores. La columna usa valores DateTime
con formato de fecha y hora para ordenar correctamente entre meses.

La petición GetAttachment de 0.3.1 enviaba AttachmentShape sin BaseShape.
ResponseShape exige ese miembro; se incluye AllProperties, conservando
IncludeMimeContent y el identificador opaco original. OwaAttachmentContract
verifica la solicitud con DataContractJsonSerializer y reproduce el 400 anterior.
El fixture HTTP exige ahora también este contrato, además del de búsqueda.
El flujo Windows recorre dos días inclusivos, cinco PDF reales y cinco adjuntos;
comprueba el último día y excluye el posterior. Se mantienen cero movimientos.

Referencia de la operación y sus propiedades de adjuntos:
https://learn.microsoft.com/exchange/client-developer/web-service-reference/getattachment-operation
