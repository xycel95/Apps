# Contexto de Guarda

## Objetivo acordado

Aplicación de escritorio para Windows que copie una carpeta a otra ubicación —pendrive, disco u otra carpeta accesible— y añada al principio del nombre la fecha con formato `aaaa_mm_dd`.

Nombre definitivo elegido por el usuario: **Guarda**. La primera versión se llamó Copia Fácil. El usuario ha pedido publicar la app en su repositorio público `xycel95/Apps`.

## Estado actual

- Versión entregada: **1.1.1**. Ejecutable `Guarda.exe` y paquete `Guarda_v1.1.1_Completo.zip` en `descargas/`. Se conservan los ZIP históricos 1.1.0 y 1.0.1.
- Código WinForms/C# para .NET Framework 4.8, AnyCPU, sin dependencias de SSH Backup.
- Copias completas, fecha local en el nombre, sufijos por repeticiones, verificación SHA-256, progreso, cancelación e informes.
- Las preferencias de Copia Fácil se recuperan como alternativa a las de Guarda.
- Diseño nuevo, modo oscuro y tema claro con preferencia guardada.
- Botón Actualizaciones: búsqueda manual, descarga y reinicio conservando preferencias. Se comprueban firma, versión, identidad, tamaño y SHA-256.
- Canal `actualizaciones/stable.xml` con firma `stable.xml.sig`, vinculado a `descargas/versiones/1.1.1/Guarda.exe`. Las siguientes versiones deben conservar el protocolo y la clave pública.
- No tiene SSH, tareas programadas, copia incremental ni reanudación. No instala ni busca actualizaciones silenciosamente al iniciar.
- Huellas en `descargas/SHA256SUMS.txt` y `app.json`.
- Clave privada guardada fuera de GitHub en el archivo privado **Guarda_Clave_de_Firma_PRIVADA.zip**. No publicarla, regenerarla ni sustituir la clave pública. Ver `docs/PUBLICAR-ACTUALIZACION.md`.

## Comprobado y pendiente

La entrega 1.1.1 se compila con Microsoft Roslyn en Windows Server 2025. La ejecución nativa pasa 14 pruebas de copias y 13 del actualizador; los dos casos adicionales de enlaces de Linux no se ejecutan en Windows. La ventana completa una copia real y comprueba ambos temas y su persistencia. El ciclo de actualización entre procesos cierra la versión anterior, sustituye el ejecutable y abre la nueva ventana. Defender activo no informa de amenazas para el SHA-256 publicado. Ver `docs/VALIDACION-WINDOWS.json` y la ejecución https://github.com/xycel95/Apps/actions/runs/41263748218.

Los correos de GitHub «All jobs have failed» de las ejecuciones anteriores correspondían a fallos en las comprobaciones: captura incompleta de errores y prueba gráfica sin el bucle normal de mensajes; algunos servidores también carecían de Defender disponible. La ejecución final completa es correcta. Esto es distinto del bloqueo de descarga original del usuario, cuyo motivo sigue sin confirmarse. El EXE no tiene firma Authenticode; la firma RSA del canal autentica las actualizaciones.

Pendiente: equipo Windows del usuario, pendrive físico, selector de carpetas con interacción humana, permisos y bloqueos específicos, rutas de red, FAT32/exFAT/NTFS y desconexiones. El transporte de pruebas y la recuperación mediante GitHub no sustituyen una consulta HTTPS desde el equipo del usuario. Ver `docs/VALIDACION.txt`.

## Para continuar

Leer `AGENTS.md` y las notas de parche; confirmar la revisión actual de la rama `guarda`. Trabajar en esta carpeta y mantener el catálogo de `main` como índice, sin fusionar las aplicaciones en él. Compilar y validar lo que cambie y coordinar versión, descargas, hashes y documentación cuando haya una nueva entrega.
