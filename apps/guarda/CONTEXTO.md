# Contexto de Guarda

## Objetivo acordado

Aplicación de escritorio para Windows que copie una carpeta a otra ubicación —pendrive, disco u otra carpeta accesible— y añada al principio del nombre la fecha con formato `aaaa_mm_dd`.

Nombre definitivo elegido por el usuario: **Guarda**. La primera versión se llamó Copia Fácil. El usuario ha pedido publicar la app en su repositorio público `xycel95/Apps`.

## Estado actual

- Versión entregada: **1.0.1**. Ejecutable `Guarda.exe` y paquete `Guarda_v1.0.1_Completo.zip` en `descargas/`.
- Código WinForms/C# para .NET Framework 4.8, AnyCPU, sin dependencias de SSH Backup.
- Copias completas, fecha local en el nombre, sufijos por repeticiones, verificación SHA-256, progreso, cancelación e informes.
- Las preferencias de Copia Fácil se recuperan como alternativa a las de Guarda.
- No tiene SSH, tareas programadas, copia incremental, reanudación ni actualizaciones en línea.
- El paquete y el ejecutable publicados conservan los bytes de la entrega 1.0.1. Sus huellas se registran en `descargas/SHA256SUMS.txt` y `app.json`.

## Comprobado y pendiente

La versión inicial del motor superó 16 pruebas con archivos reales bajo Linux/Mono. La versión 1.0.1 se recompiló tras el cambio de nombre y completó una copia real desde la ventana. Se revisó el diseño normal y al tamaño mínimo. La integridad del paquete ZIP y su coincidencia con el ejecutable se comprobaron localmente.

Sigue pendiente validar en Windows real, con pendrive físico, selector de carpetas nativo, permisos, bloqueos, resolución de rutas de Windows, rutas de red, fechas en FAT32/exFAT/NTFS y desconexiones físicas. No afirmar que las comprobaciones en Mono sustituyen esas pruebas. Detalles en `docs/VALIDACION.txt`.

## Para continuar

Leer `AGENTS.md` y las notas de parche; confirmar la revisión actual de la rama `guarda`. Trabajar en esta carpeta y mantener el catálogo de `main` como índice, sin fusionar las aplicaciones en él. Compilar y validar lo que cambie y coordinar versión, descargas, hashes y documentación cuando haya una nueva entrega.
