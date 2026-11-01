# SSH Backup

Copias locales de carpetas remotas por SSH/SFTP para Windows 10/11 y .NET Framework 4.8. **Versión 2.2.1 · 1 de noviembre de 2026.**

1. [Descripción del programa](DESCRIPCION.md)
2. [Cómo funciona y guía](docs/README.md)
3. [Descargar](descargas/README.md)
4. [Notas de parche](notas-de-parche/README.md)

## Actualizaciones desde la app

En **Herramientas → Actualizaciones**, pulsa **Comprobar actualizaciones**, revisa las novedades y elige **Instalar actualización**. El canal oficial ya está preparado. La descarga muestra su progreso, permite cancelar y se valida con firma RSA y SHA-256 antes de instalar.

Para este primer salto desde 2.0/2.1, termina las copias, cierra el programa y sustituye `SSHBackup.exe` por el nuevo en su ubicación habitual. Con el mismo usuario Windows se conservan los datos. La entrega 2.1 tiene un fallo en su instalador antiguo; desde 2.2 el canal y la instalación desde la app quedan preparados.

Se conservan los servidores, accesos, huellas, historial, tema y ajustes. Espera a que terminen las copias; mantén la ubicación del EXE y el mismo usuario Windows. El ejecutable anterior se conserva como `.previous`; se restaura si falla el lanzamiento de la nueva versión.

## Funciones

- Descarga SFTP de solo lectura, alias, importación/exportación CSV/XLSX y acceso con contraseña o clave.
- ZIP completos verificados, reanudación y novedades opcionales; los ZIP del origen se guardan íntegros.
- Programación opcional por cualquier combinación de días; requiere PC encendido y sesión iniciada.
- Comparación por hashes, búsqueda por nombre/ruta y límite opcional agregado de descarga SSH.
- Modo claro/oscuro, canal de actualizaciones preparado y paquetes firmados sin conexión.

[Paquete completo 2.2.0](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_Completo_2.2.1.zip) · [Novedades en la web](https://xycel95.es/aplicaciones/ssh-backup/) · [Metadatos](app.json)

## Desarrollo y comprobaciones

[Código y licencias](Codigo/) · [Contexto](CONTEXTO.md) · [Validación 2.2.0](docs/VALIDACION-2.2.1.md) · [Publicar una versión](PUBLICAR-ACTUALIZACION.md)

El EXE incorpora sus dependencias. Para compilar en Windows usa `Codigo/compilar.ps1`. La compilación Linux utiliza Roslyn y las referencias de Framework indicadas por `SSHBACKUP_DOTNET_ROOT`, `SSHBACKUP_MONO_ROOT` y `SSHBACKUP_NET48_REFS`; `winres.py` comprueba el manifiesto nativo. Mono se utiliza para las pruebas locales. La clave privada de firma se conserva fuera de GitHub.

La entrega original 2.1.0 y sus PDF se conservan como históricos. Las pruebas y límites de cada entrega se describen en su informe; no se confunden los ensayos Linux/Mono con Windows nativo ni con los servidores reales del destinatario.
