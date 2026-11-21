# Continuar CorreoDiario

Rama permanente `correo-diario`; carpeta `apps/correo-diario/`.

## Versión 0.4.5: copiar el error sin depender del registro

La captura de 0.4.4 avanza hasta un correo intermedio y falla con diferencia
de longitud de un carácter. Se pidieron los datos PdfFailure, pero el usuario
había borrado el registro. Petición explícita: botón para copiar el error.
No afirmar la causa del carácter perdido ni quitar la verificación sin datos.

ErrorDialog sustituye los avisos de SafeAsync y ThreadException. Muestra el
informe completo en un TextBox de solo lectura, con Copiar error y feedback.
MainForm conserva únicamente el último informe en memoria y añade Copiar
último error. Disponible hasta cerrar el programa; no requiere leer el JSON
ni crea un archivo adicional. Si falla el portapapeles, permite seleccionar
y copiar manualmente. No transmite informes automáticamente.

ErrorReport.Build incluye versión/sistema, mensaje mostrado y exclusivamente
ExportErrorContext/PdfRenderFailure/PdfTextCheck de las excepciones. No volcar
Exception.Data completo, stack ni mensajes arbitrarios de excepciones internas.
El aviso puede incluir rutas de destino y nombres de archivo ya presentes en
el error; no incluye el cuerpo del correo ni cabeceras de sesión.

PdfTextCheck añade códigos Unicode aislados, categoría y tipo de primera
diferencia sobre la secuencia normalizada, sin palabras ni fragmentos del
cuerpo. Distingue omisión, añadido, cambio y diferencia más extensa. NO modifica
la normalización ni la aceptación del PDF. Un selector de variante omitido
se diagnostica y sigue fallando; no usarlo para dar por probado el caso real.

92 pruebas; reporte disponible después de borrar su registro, lista cerrada
de datos copiados, código invisible y cifra cambiada. CopyErrorCheck compila
el diálogo real y pulsa su botón, comprobando todo el texto Unicode en el
portapapeles del escritorio CI; exige GITHUB_ACTIONS=true. El arranque del EXE
comprueba Copiar último error deshabilitado antes del primer fallo. Consultar
VALIDACION-WINDOWS.json para el commit y resultado. Históricos en docs/historico/0.4.4.

## Corrección 0.4.4: segunda composición verificada y diagnóstico

Nueva captura de 0.4.3: persiste el rechazo del texto, cero movimientos. No
se dispone del correo real ni del PDF que falla. No afirmar su causa exacta
ni dar el caso por resuelto por pasar pruebas sintéticas.

IVerifiedPdfRenderer devuelve bytes y verificación. PdfRenderer intenta primero
el formato habitual. Solo ante discrepancia de texto genera una segunda
composición a partir del MISMO documento local: texto codificado como literal
y las imágenes incluidas al final, sin reutilizar el PDF rechazado. Materializa
los números de las listas antes de capturar el texto para no perderlos al
pasar a texto sencillo. Indica el formato simplificado en el PDF y en el aviso
de finalización. No usa el PDF para construir el texto esperado.

La segunda composición exige igualdad de TODA la secuencia normalizada de
letras, marcas combinadas y cifras. No se acepta por porcentaje ni por una
bolsa de caracteres. Si vuelve a fallar, no comienza ningún movimiento.
Los adjuntos siguen sujetos a la segunda lectura y SHA-256 y todos los archivos
quedan directamente en el destino con nombres sin sobrescritura.

PdfVerifier conserva InvalidDataException y adjunta PdfTextCheck en Data ante
discrepancias. Son recuentos y posición de primera diferencia; no copia palabras
ni fragmentos del cuerpo. ExportReceipt incluye PdfFailure y FailedMessageNumber
si falla, y SavedMessage.Pdf si se completa. El registro habitual sigue incluyendo
los identificadores/asuntos de las copias: no subir un registro real completo
al repositorio público. PdfFailure por sí solo contiene únicamente números/flags.

88 pruebas. PdfReflowCheck usa el renderizador real y ExportEngine: reproduce
un rechazo restante de 0.4.3 (palabra partida junto con marcadores de lista),
compone y coteja 60 registros en varias páginas, listas romanas/alfabéticas,
imagen CID, fichero adjunto dos veces, registro y cero movimientos. También
rechaza una cifra cambiada. Consultar VALIDACION-WINDOWS.json para resultado
y commit de publicación. Validaciones previas en docs/historico/0.4.3.

## Corrección 0.4.3: cotejo del PDF

La captura de 0.4.2 muestra un rechazo del texto del PDF, con cero movimientos.
No se dispone del correo/PDF original. Se reproduce el mismo rechazo con
palabras completas que la extracción del PDF parte en líneas o páginas.
El cotejo anterior comparaba solo bolsas de palabras. Se conserva ese cotejo
y, cuando falla, se admite exclusivamente la igualdad de toda la secuencia
de letras/cifras normalizadas, en orden y cantidad. No introducir porcentajes
de similitud, tolerancias de caracteres ni desactivar la verificación.

PdfRenderer elimina anchos/alturas/overflow restrictivos del cuerpo, adapta
tablas a A4 y fuerza el formato de impresión antes de recoger innerText.
La composición es fluida; no es una réplica visual del HTML de Outlook.
83 pruebas de lógica (dos regresiones fallan antes de la corrección). El
WindowsCheck usa el mismo PdfRenderer.cs: debe demostrar el falso rechazo del
algoritmo anterior con un PDF real, luego conservar tablas anchas de 90 filas,
texto final, Unicode, listas e imágenes CID. También rechaza contenido omitido.
No afirmar que se ha verificado el correo concreto de la captura.
La publicación solo se genera si las pruebas nativas terminan correctamente;
consultar VALIDACION-WINDOWS.json para su resultado y commit de origen.
Validaciones anteriores conservadas en docs/historico/0.4.2.

## Versión 0.4.2: todos los archivos juntos

El usuario indica que 0.4.1 parece funcionar. Las tres carpetas que muestra
corresponden a ejecuciones distintas; la aplicación creaba una por intento.
Requisito vigente: guardar todos los PDF y adjuntos directamente en el destino
elegido, sin subcarpetas por ejecución. No volver a introducir ese agrupamiento.

ExportEngine usa el destino directo y reserva un nombre común por mensaje.
Comprueba archivos anteriores, incluidas copias parciales con solo adjuntos.
Reserva PDF con CreateNew y añade sufijo numérico si ya existe. Los adjuntos
usan CreateNew, conservando cualquier archivo de una carrera posterior.
Los registros son Comprobacion_fecha_hora_id.json, independientes por intento,
reservados sin sobrescribir y con temporales propios. ReceiptPath está en el
resultado y en errores; no buscar un único Comprobacion.json por directorio.

75 pruebas; incluye repetición en un mismo destino, conservación de bytes y
registros, adjuntos previos sin PDF y dos ejecuciones simultáneas. El flujo
Windows con cinco PDF/cinco adjuntos comprueba destino directo sin subcarpetas.
No se han movido ni eliminado las carpetas que el usuario ya tenía en su PC.
Se mantiene verificación de contenido, rango y borrado opcional desmarcado.
Validación de 0.4.1 conservada en docs/historico/0.4.1.

## Corrección 0.4.1: tamaño de almacén frente a contenido

La captura de 0.4 muestra que GetAttachment ya responde; se detiene porque
el motor comparaba AttachmentInfo.Size con los bytes descargados. Microsoft
documenta PidTagAttachSize como contenido más propiedades del objeto. No
introducir tolerancias de tamaño ni tratar esa cifra como longitud exacta.
OWA y clásico etiquetan Size como OutlookStoreBytes; ContentBytes conserva
la igualdad estricta para fuentes que realmente proporcionen longitud de fichero.

El motor conserva cada copia, repite lectura del origen para todos los adjuntos
y exige longitud y SHA-256 iguales antes de movimientos. También conserva
la revisión de snapshots y de todos los bytes en disco. No reintenta movimientos.
SavedAttachment registra tamaños informado/real, semántica, SHA y resultado de
la segunda lectura. Content ausente falla; Content vacío explícito puede
representar un fichero de cero bytes aunque su objeto tenga propiedades.

71 comprobaciones. El fixture reproduce tamaño de almacén 344 frente a 8 bytes
de contenido y exige diez lecturas para cinco adjuntos, cinco PDF reales y cero
movimientos. Pruebas de cambios de misma longitud, truncado, contenido vacío
y falta de Content. Las cuentas reales y el perfil clásico siguen pendientes.
Se conserva Desde/Hasta de 0.4 y sus comprobaciones. Informes de 0.4 archivados.
No subir las capturas ni datos personales o de correos al repositorio público.

## Versión 0.4: adjuntos e intervalos

El usuario confirma por captura que 0.3.1 busca correos reales. Falla GetAttachment
con 400 antes de exportar; el aviso indica cero movimientos. AttachmentShape
omitía BaseShape, requerido por ResponseShape. Se añade AllProperties y se
mantienen IncludeMimeContent e identificadores originales. No cambiar de nuevo
la sesión del navegador ni pedir al usuario contraseñas o datos de sesión.
OwaAttachmentContract es un lector independiente del contrato; rechaza el formato
antiguo. El servidor de prueba también lo exige, además del contrato de búsqueda.

Requisito nuevo: Desde/Hasta, ambos días completos. MailDateRange centraliza
validación, medianoches UTC y pertenencia para OWA y Outlook clásico. Hoy pone
ambas fechas actuales. Fecha y hora en la tabla, con DateTime para ordenar;
cambiar fechas limpia selección y desmarca borrado. No exportar datos de una
consulta anterior cuando cambien las fechas. No se limita artificialmente el rango.

65 comprobaciones; incluye intervalos invertidos, límites, cambios de hora,
contrato de adjuntos e identificador con caracteres especiales. Flujo Windows
con Edge y servicio HTTPS sintético: dos días, cinco PDF reales de WebView2,
cinco adjuntos y cero movimientos. Distinguir de exportación en el servidor real,
que sigue pendiente. La interfaz nativa comprueba Desde/Hasta y dos calendarios.
Conservar validaciones de 0.3.1 en docs/historico/0.3.1.

## Corrección 0.3.1: error 400 al buscar correos

La captura del usuario confirma que 0.3 conecta y lista las carpetas reales.
La búsqueda por fecha fallaba con 400: Restriction debe envolver la expresión
And en Item; el discriminador JSON de las constantes es Constant:#Exchange,
no ConstantValue:#Exchange. No cambiar de nuevo el mecanismo de autenticación.
FindItem devuelve resúmenes; se solicita DisplayTo y GetItem conserva el acceso
a los destinatarios completos durante la exportación.

59 comprobaciones, incluyendo contrato de búsqueda leído con el serializador
DataContractJsonSerializer independiente, formatos antiguos rechazados, días
locales de 23/24/25 horas, Enviados y carpetas adicionales. El servicio HTTPS
sintético reproduce 400, exige el contrato y fuerza paginación. WindowsChecks
compila el PdfRenderer.cs real y verifica cuatro PDF de WebView2 y cuatro
adjuntos; borrado desmarcado, cero movimientos. Diferenciar este flujo del
servidor y la cuenta real, cuya exportación sigue pendiente. No subir la captura,
nombres de carpetas reales ni otros datos personales al repositorio público.
La lista anterior se limpia antes de una búsqueda para evitar reutilizar una
selección si falla la nueva consulta. Los errores HTTP indican la operación.
Conservar informes anteriores en docs/historico/0.3.

## Corrección 0.3: error 401 reportado por el usuario

La versión 0.2 detectó la pestaña real, pero Outlook rechazó la lectura con 401.
El transporte usaba cookies/CSRF y no reutilizaba Authorization ni el contexto
real de servicio. No atribuir el fallo a una contraseña del usuario.

0.3 observa solicitudes OWA correctas exclusivamente de la sesión CDP y marco
principal elegidos. Reutiliza Authorization (Bearer/MSAuth1.0), cabeceras de
sesión permitidas y destino HTTPS observado; no persiste credenciales ni copia
bases del perfil. El permiso del navegador sigue siendo más amplio que la
pestaña. Una lectura puede reintentarse una vez tras autorización renovada;
MoveItem nunca se reintenta. La sesión se conserva tras un error de carga para
que Actualizar en Outlook permita repetir Usar esta pestaña. No forzar recargas.

50 comprobaciones de lógica y una regresión con Edge real/servicio HTTPS
sintético; distinguir de autenticación contra Microsoft, exportación y cuentas
reales, que siguen pendientes. Corregidos contraste de botones deshabilitados,
texto de casilla y espacio del subtítulo. Conservar informes históricos de 0.1/0.2.

## Requisito vigente desde 0.2

El usuario rechazó iniciar sesión dentro del programa. Quiere utilizar la
ventana de navegador que ya tiene abierta. No reintroducir la autenticación
integrada. La versión 0.2 conecta con Edge/Chrome usando el descriptor local
DevToolsActivePort y el permiso del navegador; no instala una extensión ni
abre un perfil nuevo. Selecciona una pestaña HTTPS de Outlook y comprueba su
identidad/origen antes de actuar. No lee las bases de cookies o contraseñas.
El permiso técnico es de sesión del navegador, aunque la aplicación solo opera
sobre la pestaña elegida; mantener esa explicación clara en la guía.
WebView2 queda únicamente para la composición local de PDF. Conservar el
modo clásico y las garantías del borrado opcional. Los servidores laborales
y el control del navegador real siguen pendientes hasta una prueba del usuario.

## Antecedentes de la primera beta
Leer este archivo, `app.json`, las notas y la revisión remota antes de editar.

El usuario pidió un EXE que pueda abrir desde una carpeta compartida para
guardar al final del día los mensajes y adjuntos. Quiere selección de carpetas,
incluido Enviados, PDF por mensaje y adjuntos originales separados. Los nombres
comparten fecha, hora, RECIBIDO/ENVIADO y dirección.

El borrado es opcional, con casilla desmarcada, y solo debe comenzar cuando
toda la exportación se haya completado y comprobado. La primera versión mueve
a Eliminados; no vacía la papelera. No prometer liberación inmediata de cuota.

El enlace del correo personal era de Outlook web. Se desconoce el servidor
laboral. Se añadieron conexión web configurable y conexión al Outlook clásico
instalado. No presentar cualquiera de esas conexiones como probada con las
cuentas del usuario. No registrar aplicaciones Entra ni cambiar el centro de
trabajo como parte de una publicación.

Código: .NET 10, WinForms, WebView2 y PdfPig. El acceso web usa JSON interno de
OWA en la sesión del usuario; puede fallar según la cuenta. El modo clásico
usa el modelo de objetos del perfil local y no controla el nuevo Outlook.

La entrega inicial pasó 23 comprobaciones en Linux y compiló para Windows.
Una prueba adicional de Chromium no pudo arrancar en ese entorno y no se
contabiliza como superada. Consulta `docs/VALIDACION-WINDOWS.json` para las
pruebas realizadas en la publicación, sin confundirlas con autenticación real
ni con impresión en el equipo de uso.

La publicación usa un flujo temporal que se retira al terminar. Los binarios
se distribuyen como archivos de la publicación, fuera del historial Git.
El catálogo de `main` enlaza esta rama; no fusionar el código de la aplicación
en `main`. No hay actualizador automático configurado.
