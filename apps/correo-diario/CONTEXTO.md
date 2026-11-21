# Continuar CorreoDiario

Rama permanente `correo-diario`; carpeta `apps/correo-diario/`.

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
