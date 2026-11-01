# Contexto de Guarda

## Objetivo acordado

Aplicación de escritorio para Windows que copie una carpeta a otra ubicación —pendrive, disco u otra carpeta accesible— y añada al principio del nombre la fecha con formato `aaaa_mm_dd`.

Nombre definitivo elegido por el usuario: **Guarda**. La primera versión se llamó Copia Fácil. El usuario ha pedido publicar la app en su repositorio público `xycel95/Apps`.

## Estado actual

- Versión entregada: **1.1.0**. Ejecutable `Guarda.exe` y paquete `Guarda_v1.1.0_Completo.zip` en `descargas/`. Se conserva el ZIP histórico 1.0.1.
- Código WinForms/C# para .NET Framework 4.8, AnyCPU, sin dependencias de SSH Backup.
- Copias completas, fecha local en el nombre, sufijos por repeticiones, verificación SHA-256, progreso, cancelación e informes.
- Las preferencias de Copia Fácil se recuperan como alternativa a las de Guarda.
- Diseño nuevo, modo oscuro y tema claro con preferencia guardada.
- Botón Actualizaciones: búsqueda manual, descarga y reinicio conservando preferencias. Se comprueban firma, versión, identidad, tamaño y SHA-256.
- Canal `actualizaciones/stable.xml` con firma `stable.xml.sig`, vinculado a `descargas/versiones/1.1.0/Guarda.exe`. Las siguientes versiones deben conservar el protocolo y la clave pública.
- No tiene SSH, tareas programadas, copia incremental ni reanudación. No instala ni busca actualizaciones silenciosamente al iniciar.
- Huellas en `descargas/SHA256SUMS.txt` y `app.json`.
- Clave privada guardada fuera de GitHub en el archivo privado **Guarda_Clave_de_Firma_PRIVADA.zip**. No publicarla, regenerarla ni sustituir la clave pública. Ver `docs/PUBLICAR-ACTUALIZACION.md`.

## Comprobado y pendiente

El motor supera 16 pruebas con archivos reales en Linux/Mono. El actualizador supera 13 pruebas de firma, integridad, sustitución y restauración ante un fallo de apertura. La ventana 1.1.0 completa una copia real; se comprueban oscuro/claro, persistencia del tema y tamaño mínimo. El ZIP se valida y se compara con el ejecutable y el canal firmado.

Sigue pendiente validar el ciclo completo de cierre, sustitución y reinicio en Windows real; también pendrive físico, selector nativo, permisos, bloqueos, rutas de Windows y de red, fechas en FAT32/exFAT/NTFS y desconexiones. Mono y el transporte controlado no sustituyen esas pruebas ni una consulta HTTPS desde el Windows del usuario. Ver `docs/VALIDACION.txt`.

## Para continuar

Leer `AGENTS.md` y las notas de parche; confirmar la revisión actual de la rama `guarda`. Trabajar en esta carpeta y mantener el catálogo de `main` como índice, sin fusionar las aplicaciones en él. Compilar y validar lo que cambie y coordinar versión, descargas, hashes y documentación cuando haya una nueva entrega.
