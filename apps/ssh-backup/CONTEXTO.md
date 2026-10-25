# Contexto de SSH Backup

Actualizado el 25 de octubre de 2026 para continuar el proyecto desde GitHub.

## Decisiones vigentes

- Programa portátil Windows `.exe`, C# WinForms y .NET Framework 4.8. Está pensado para unos 50 servidores; admite alias, destinos por servidor, importación/exportación de Excel y CSV, contraseña o clave, y modo oscuro.
- Se descarga una carpeta completa por SFTP, incluidos ZIP originales y subcarpetas. No se modifica el servidor ni se descomprimen los ZIP originales.
- Configuración e historial en `%LOCALAPPDATA%\SSHBackup`; las credenciales se protegen para el usuario de Windows. No almacenar esos datos reales en GitHub.
- 2.0 incorporó reanudación, novedades opcionales, programación opcional, comparación, búsqueda, actualizaciones firmadas y limitador opcional de red.
- 2.1.0 incorporó el diseño moderno aprobado: menú lateral, paneles y botones redondeados, campos más claros y siete casillas independientes para programar cualquier combinación de días.
- La entrega 2.1.0 corresponde al 23 de octubre de 2026. Desde el 25 de octubre el proyecto se mantiene en la rama `ssh-backup` del repositorio público `xycel95/Apps`. El repositorio reúne varias aplicaciones mediante ramas independientes; `main` es el índice general.

## Archivos principales

- `Codigo/MainForm.cs`, `MainActions.cs`, `ToolsPage.cs`, `Dialogs.cs`: pantallas y acciones.
- `ModernControls.cs`, `Theme.cs`: diseño y temas. Los controles personalizados conservan los comportamientos nativos de Button y CheckBox.
- `Backup.cs`, `BackupTransfers.cs`, `RemoteFolder.cs`: lectura SFTP, descarga, reanudación, novedades, validación y ZIP completo.
- `Scheduling.cs`, `Models.cs`: selección semanal, migración y tarea de Windows.
- `ArchiveCatalog.cs`: catálogo, comparación por hashes y búsqueda de nombres/rutas.
- `Bandwidth.cs`: límite agregado del transporte SSH recibido.
- `Updates.cs`: manifiesto, firma RSA, SHA-256 y sustitución del ejecutable.
- `Tests.cs`, `Tests2.cs`, `verify.py`, `verify2.py`: comprobaciones locales.
- `descargas/README.md`: estado de la entrega. El archivo disponible es SSHBackup_Completo_2.1.0.zip, con la entrega original verificada.

## Límites conocidos

- Pruebas realizadas en Linux/Mono. Faltan Windows nativo, ejecución real de su Programador de tareas y arranque nativo tras actualizar.
- La programación usa la hora del PC y exige ordenador encendido y sesión iniciada. No se instala como servicio ni despierta el ordenador.
- El límite de red es global para SSH, con pequeñas ráfagas por los búferes; no limita otras aplicaciones ni la descarga HTTPS del actualizador.
- Novedades usa nombre/ruta, tamaño y fecha remotos; cada ZIP final es independiente. Una primera copia compatible establece la base.
- Búsqueda por nombres/rutas, sin texto de documentos ni contenido de ZIP anidados.
- No hay canal de versiones online preconfigurado. El paquete firmado se descarga y abre dentro del programa. La disponibilidad pública en GitHub no configura automáticamente la URL del manifiesto de versiones.
- El actualizador guarda `SSHBackup.exe.previous` y restaura si no puede lanzar el proceso nuevo. No realiza una prueba completa de funcionamiento tras el arranque.
- En un ensayo la validación detuvo una copia al detectar una lista remota distinta; dos ejecuciones completas posteriores pasaron. Se conserva el rechazo seguro y el aviso detalla nombres añadidos o ausentes. Véase `docs/VALIDACION.txt`.

## Fuera de GitHub

La clave privada para firmar actualizaciones se conserva fuera del repositorio, bajo control de su responsable. No está en esta carpeta ni en el ZIP de entrega. Las claves SSH, contraseñas, configuraciones reales y datos de las copias también quedan fuera.

## Próximo trabajo

La migración no cambia el programa ni su versión. Para cambios nuevos, leer las fuentes actuales de esta rama, preparar el cambio solicitado y verificarlo antes de distribuir. No suponer que una subida a GitHub ha creado un canal automático de versiones ni modificado otros servicios.

## Comprobaciones de migración del 25 de octubre de 2026

- La primera revisión encontró las ramas `main` y `ssh-backup` apuntando al mismo commit, con solo la portada y las instrucciones generales. La rama estaba creada, pero la aplicación aún no se había publicado.
- Se ha recuperado la entrega original 2.1.0 y el proyecto preparado para publicar. Los 74 archivos del manifiesto coincidían con sus hashes antes de actualizar esta documentación.
- El ZIP supera la comprobación CRC de todos sus archivos y conserva el SHA-256 `d4b4eae3c04395aa0c2aab2df6bde746978403c5229943f9ca8adda3deb741ed`. El EXE incluido conserva el SHA-256 `2b9936b40e9a7676d38359db0921b96100ea892a61b32523de4bf40ff47358c9`.
- El código y el ejecutable no han cambiado durante la migración. Se reutiliza la validación documentada en `docs/VALIDACION.txt`; siguen pendientes las pruebas en Windows nativo.
- La portada general enlaza la rama de esta aplicación, su descarga y la guía. Las futuras aplicaciones deben incorporarse en ramas propias siguiendo la misma organización.

## Organización vigente desde el 25 de octubre de 2026

El propietario ha pedido gestionar todas sus aplicaciones desde el repositorio Apps, con una rama permanente para cada programa. SSH Backup conserva la rama ssh-backup y la carpeta apps/ssh-backup.

- DESCRIPCION.md explica el propósito y los requisitos.
- docs/README.md introduce el uso y enlaza la guía completa original.
- descargas/README.md muestra los archivos disponibles y lo pendiente.
- notas-de-parche/README.md conserva las notas, con las versiones nuevas arriba.
- app.json reúne los datos y enlaces públicos de la aplicación; download.available solo debe ser true cuando el paquete exista y se haya comprobado su descarga.
- Codigo y las licencias existentes se conservan sin modificar el programa ni su versión.

La portada de main actúa como índice de aplicaciones. No fusionar automáticamente el código de una rama de aplicación en main.

## Estado actual de la entrega descargable

La entrega original **2.1.0**, del 23 de octubre, se recuperó y publicó el 25 de octubre de 2026. El bloqueo anterior por falta del ZIP queda resuelto. No se ha recompilado el programa ni creado una versión nueva.

- Paquete completo: [SSHBackup_Completo_2.1.0.zip](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_Completo_2.1.0.zip), **10.013.485 bytes**.
- ZIP SHA-256: `d4b4eae3c04395aa0c2aab2df6bde746978403c5229943f9ca8adda3deb741ed`.
- EXE SHA-256: `2b9936b40e9a7676d38359db0921b96100ea892a61b32523de4bf40ff47358c9`.
- Los 72 archivos del ZIP superan la comprobación CRC. El paquete SSHBackup_2.1.0.sshupdate contiene el mismo ejecutable; su manifiesto se ha verificado con RSA PKCS1v15 y SHA-256 usando la clave pública existente.
- Se han incorporado el ejecutable, el paquete y manifiesto firmados, las DLL originales, los recursos, las guías PDF, la captura de programación y la plantilla XLSX. Las licencias y el código existentes se conservan. Siete archivos de texto solo difieren del ZIP en BOM o saltos de línea; se mantienen sus copias ya publicadas.
- La descarga desde GitHub se comprueba contra el hash original. `app.json`, las portadas y el apartado de descargas muestran la entrega disponible. `descargas/SHA256SUMS.txt` recoge los hashes de los archivos descargables.
- No se han incluido claves privadas ni configuraciones personales. La fuente privada utilizada para recuperar la entrega no se publica en el repositorio.

Las comprobaciones de esta recuperación son de integridad, firma y publicación. Se mantienen las limitaciones de las pruebas históricas Linux/Mono: Windows nativo, el Programador de tareas y el arranque tras actualizar siguen pendientes. La publicación no configura un canal online del actualizador ni modifica WordPress, Minecraft o Pterodactyl.

## Continuar a partir de aquí

La entrega completa ya está en esta rama; no hace falta abrir las conversaciones antiguas ni volver a pedir el ZIP al propietario. Para una actualización futura, seguir `PUBLICAR-ACTUALIZACION.md`, conservar los bytes de las entregas anteriores mientras se valida la nueva y añadir las notas de la nueva versión arriba del historial. Leer siempre el estado remoto actual antes de escribir.

La revisión automática había rechazado dos intentos antiguos de publicación por falta de autorización para un archivo y su destino público. El propietario autorizó expresamente publicar el programa y sus dependencias en Apps. La recuperación y publicación actuales se han realizado por las vías normales de los conectores, sin modificar permisos ni eludir rechazos.
