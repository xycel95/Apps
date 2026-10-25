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
- `descargas/SSHBackup_Completo_2.1.0.zip`: entrega original íntegra, con EXE y paquete `.sshupdate`.

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

## Estado pendiente de la publicación binaria

El código y los documentos de texto ya se han publicado en `ssh-backup`. No se ha cambiado el código de la aplicación ni la entrega 2.1.0. El ZIP, las DLL, las plantillas y los PDF todavía no se han incorporado al árbol de la rama.

La revisión automática ha rechazado dos veces una parte de `BouncyCastle.Cryptography.dll` al exigir autorización explícita del usuario para el archivo y el destino público. Antes del segundo intento se verificó que la DLL era idéntica a `lib/net461/BouncyCastle.Cryptography.dll` del paquete oficial de NuGet 2.7.0, y que el paquete coincidía con el hash del archivo de dependencias. Esa comprobación no desbloqueó la acción. No continuar ni cambiar de vía de subida hasta resolver esa autorización.

Se ha retirado el flujo temporal de reconstrucción de binarios; no hay ninguna publicación pendiente que vaya a ejecutarse automáticamente. La portada general y esta rama muestran el estado real de la descarga. La entrega original permanece preparada para continuar tras resolver el bloqueo.
