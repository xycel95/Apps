SSH BACKUP 2.1.0 - GUÍA DE USO

DISEÑO RENOVADO
Menú lateral, botones redondeados, controles y campos más claros, y tipografía con mejor jerarquía. El cambio se aplica a Copias, Servidores, Historial, Ajustes, Herramientas y los diálogos. El modo claro y oscuro se conservan. La programación permite marcar cada día de la semana por separado.
Windows 10/11. Requiere .NET Framework 4.8. No necesita Python ni Excel.

ACTUALIZAR DESDE 1.2.0
Espera a que terminen las copias, cierra el programa y sustituye SSHBackup.exe en su ubicación habitual. Abre el EXE con el mismo usuario de Windows: se mantienen servidores, credenciales cifradas, huellas, historial y tema.

PRIMERA COPIA
1. En Servidores, añade registros o utiliza Importar listado con Excel (.xlsx) o CSV. Una fila por servidor: alias, host, puerto, usuario, contrasena, carpeta_origen, carpeta_destino.
2. El alias es único y da nombre a la subcarpeta de destino. El destino de cada fila es una carpeta base del PC; si se deja vacío se usa el destino general de Ajustes.
3. Comprueba SSH y verifica la huella que presenta el servidor antes de aceptarla. El programa conserva esa identidad.
4. Marca Copiar en los servidores elegidos. En Copias, pulsa Iniciar copia. Si falla un servidor, los demás continúan.
5. Revisa el visor, el registro y el historial. Solo se publica un ZIP terminado tras leerlo completo y verificar su contenido con SHA-256.

QUÉ SE COPIA
Se leen todos los archivos normales y subcarpetas del origen, incluidos ZIP originales, nombres con espacios/tildes y carpetas vacías. Los ZIP de origen se guardan como archivos, sin descomprimirlos ni alterarlos. Los enlaces simbólicos y archivos especiales se omiten con un aviso; las exclusiones de Ajustes se anotan en el manifiesto.
El servidor se usa solo para lectura SFTP: no se comprime, borra, renombra ni escribe en el origen.

REANUDAR UNA DESCARGA
Si detienes una copia o se corta la conexión, la descarga se conserva en .sshbackup-resume, dentro de la carpeta del alias. Pulsa Reintentar fallidos o inicia de nuevo el lote con el mismo servidor, origen y destino. Se recuperan los archivos terminados y la parte descargada del archivo interrumpido, siempre que tamaño y fecha sigan coincidiendo.
Si cambia el archivo remoto, se descarta su parcial. Si cambia el acceso, origen, destino, huella o exclusiones, puede ser necesaria una descarga nueva. La caché no contiene contraseñas; sí contiene los datos descargados. Se limpia después de publicar la copia verificada. El cierre puede esperar a que finalice la operación SFTP en curso o venza el tiempo de espera configurado.

COPIAR SOLO NOVEDADES - OPCIONAL
La casilla está en Copias y comienza desmarcada. Activada, reutiliza desde una copia 2.0 anterior los archivos con el mismo nombre/ruta, tamaño y fecha de modificación, comprobando sus hashes locales. Descarga los nuevos y modificados. Si la copia anterior no es utilizable, vuelve a descargar el original.
Cada ZIP nuevo es una copia completa e independiente: no necesita conservar el ZIP anterior para abrirlo. Las copias anteriores no se borran. La primera copia 2.0 establece la base; las de 1.x pueden consultarse, pero no se reutilizan para este modo.
Si un programa modifica contenido conservando a la vez el tamaño y la fecha remotos, este modo no puede detectarlo solo con metadatos: desmarca la casilla para forzar la descarga completa.
El visor distingue descargado por SSH, reutilizado sin cambios y recuperado de una descarga anterior. Se necesita espacio para los datos temporales sin comprimir y el ZIP final.

LÍMITE DE VELOCIDAD DE RED - OPCIONAL
En Ajustes, activa Limitar la descarga total de la aplicación, elige una cantidad y la unidad (Mbps o MB/s) y pulsa Guardar ajustes. Empieza desactivado. Por ejemplo, 8 Mbps equivale a 1 MB/s; 10 Mbps equivale a 1,25 MB/s.
Es un límite global de descarga SSH compartido entre todos los servidores: no se multiplica por las conexiones simultáneas. Se aplica a las siguientes copias, incluidas las programadas, y queda guardado para tu usuario. Los archivos recuperados o reutilizados localmente no consumen descarga SSH.
El transporte regula el flujo recibido, incluidos los datos SSH, para no depender solo del avance al escribir en disco. El sistema puede recibir pequeñas ráfagas por sus búferes; el tráfico de control TCP/IP y otras aplicaciones no queda limitado. No limita la descarga de paquetes de actualización por HTTPS. Para cambiarlo, espera a que termine el lote, modifica Ajustes y guarda.

BUSCAR Y COMPARAR
En Herramientas > Buscar y comparar, pulsa Actualizar catálogo. Se leen las copias del historial y de las carpetas de destino de los servidores actuales. Añadir ZIP permite incluir copias antiguas o trasladadas.
Para comparar: elige dos copias del mismo alias, servidor y origen y pulsa Comparar. Se muestran archivos añadidos, eliminados y modificados, además de carpetas añadidas/eliminadas. Los hashes permiten detectar cambios de contenido aunque coincidan tamaño y fecha. El programa ordena cronológicamente las dos copias.
Para buscar: escribe parte del nombre o ruta y pulsa Buscar en todas. Los resultados indican servidor, fecha, ruta y tamaño. La búsqueda no distingue mayúsculas. Un doble clic abre el ZIP correspondiente. Se buscan las rutas guardadas en las copias, no el texto de los documentos ni el interior de ZIP originales anidados. Si hay demasiados resultados, concreta la búsqueda.

COPIAS PROGRAMADAS - OPCIONAL
En Herramientas > Programación, activa la casilla, elige la hora y marca los días de la semana que quieras: cada día tiene su propia casilla. Puedes elegir cualquier combinación, por ejemplo lunes y miércoles o solo domingo. Todos los días marca las siete casillas y Ninguno las desmarca. El resumen muestra la selección y la hora antes de guardar. Pulsa Guardar programación. Desmarca la activación y guarda para desactivarla. No se permite activar sin seleccionar al menos un día. Las configuraciones de la versión 2.0 mantienen automáticamente su selección anterior (todos los días o de lunes a viernes).
Antes de activarla, marca los servidores, guarda sus accesos y usa Comprobar SSH para que sus huellas ya estén aceptadas. No se aceptan automáticamente identidades nuevas o cambiadas durante una ejecución programada.
Se crea una tarea para tu usuario en el Programador de tareas de Windows. Puede abrir el EXE aunque lo hubieras cerrado. El PC debe estar encendido y tu sesión iniciada. No guarda la contraseña de Windows ni requiere una sesión del programa abierta. Las políticas del equipo pueden impedir crear tareas: en ese caso se muestra el error y no se anuncia como activada.
Si el programa está abierto al llegar la hora, usa esa misma instancia; si está copiando, espera a que quede libre. Como máximo se inicia un lote programado por día. Si se perdió la hora, se recupera al abrir o al lanzar Windows la tarea ese día. El horario usa la zona del propio PC.
Mantén el EXE en una ubicación fija. Si lo mueves, guarda otra vez la programación. Los errores de preparación se guardan también en programacion.log, en la carpeta de configuración.

ACTUALIZADOR INTEGRADO
La versión 1.x se actualiza manualmente una primera vez. A partir de 2.0, Herramientas > Actualizaciones > Abrir paquete permite cargar un archivo .sshupdate recibido por correo o descargado de Drive. Muestra la versión y las novedades. Solo habilita instalar si es una versión posterior y el paquete supera la firma RSA y SHA-256.
Al instalar, termina primero cualquier copia. El programa se cierra, conserva SSHBackup.exe.previous, sustituye el EXE y abre la versión nueva. Si no puede iniciar el proceso nuevo, restaura el archivo anterior. Los datos de configuración se conservan en su carpeta habitual del usuario.
La distribución actual es privada. No hay un canal público ni una URL online preconfigurada. Opcionalmente puedes introducir la URL HTTPS de un manifiesto de versiones firmado y pulsar Comprobar novedades; si la nueva versión incluye una URL de paquete, puede descargarla desde el propio programa. Una página de Drive que requiera iniciar sesión no sirve como manifiesto directo.
No se instala nada automáticamente sin pulsar Instalar actualización y confirmar el cierre.

OTRAS FUNCIONES
Servidores > Exportar listado conserva toda la lista en Excel o CSV, incluidos servidores no marcados para copiar. Las contraseñas solo se incluyen si se activa su casilla; en ese caso el archivo exportado contiene secretos visibles.
Modo oscuro se guarda por usuario. Historial permite abrir los ZIP, su carpeta y el registro. Ayuda incluye esta guía.

CONFIGURACIÓN Y COPIAS
La configuración está en %LOCALAPPDATA%\SSHBackup. Contraseñas cifradas para el usuario Windows actual. No copies configuracion.xml a otro usuario esperando recuperar sus contraseñas: utiliza Exportar/Importar y revisa rutas de claves y destinos.
Los registros del destino general están en Registros. Conserva el ZIP final y las copias anteriores según tu política. El programa no aplica un borrado automático de copias.

VALIDACIÓN
Probado en Linux/Mono con un servidor SSH/SFTP local y pruebas de interfaz. La ejecución nativa en Windows, las políticas del Programador de tareas y el arranque posterior a la instalación deben comprobarse también en el equipo de destino. No se han utilizado servidores reales del usuario para los ensayos.
