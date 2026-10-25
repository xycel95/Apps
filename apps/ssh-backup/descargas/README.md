# Descargar SSH Backup

## Paquete completo 2.1.0

**[Descargar SSH Backup 2.1.0 — ZIP completo original](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_Completo_2.1.0.zip)**

Para **Windows 10 y 11**, con **.NET Framework 4.8**. Aplicación portátil: extrae el ZIP y abre `SSHBackup.exe`. Mantén el ejecutable en una ubicación fija si utilizas la programación.

La entrega original del 23 de octubre de 2026 incluye el ejecutable, el paquete de actualización firmado, la guía, las notas de parche, la captura de programación, las plantillas y el código con sus dependencias y licencias. Publicada en GitHub el 25 de octubre sin recompilar ni cambiar el programa.

## Archivos individuales

| Archivo | Uso |
| --- | --- |
| [ZIP completo](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_Completo_2.1.0.zip) | Entrega recomendada, **10.013.485 bytes**. |
| [SSHBackup.exe](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup.exe) | Ejecutable portátil, con sus dependencias integradas. |
| [SSHBackup_2.1.0.sshupdate](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_2.1.0.sshupdate) | Paquete firmado para el actualizador del programa. |
| [SSHBackup_2.1.0.xml](https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/descargas/SSHBackup_2.1.0.xml) | Manifiesto firmado de esta entrega. |
| [SHA256SUMS.txt](SHA256SUMS.txt) | Huellas de integridad de los archivos descargables. |

## Guías y plantillas

- [Cómo funciona y guía](../docs/README.md).
- [Guía completa en PDF](../docs/SSHBackup_Guia_de_uso.pdf).
- [Notas de parche 2.1.0 en PDF](../docs/SSHBackup_Notas_del_parche_2.1.0.pdf).
- [Plantilla de servidores XLSX](../plantillas/Plantilla_Servidores_SSH.xlsx?raw=true) y [CSV](../plantillas/Plantilla_Servidores_SSH.csv?raw=true).
- [Código fuente y licencias](../Codigo/).

## Actualizar una instalación existente

Desde la versión 2.0, extrae el ZIP y abre `SSHBackup_2.1.0.sshupdate` en **Herramientas → Actualizaciones → Abrir paquete**. Espera a que terminen las copias antes de instalar.

También puedes cerrar el programa y sustituir `SSHBackup.exe` en su ubicación habitual. Con el mismo usuario de Windows se conservan los datos personales de la aplicación. Si cambias su ubicación, vuelve a guardar la programación.

No hay un canal online de actualizaciones preconfigurado. Publicar esta descarga no modifica ese ajuste.

## Integridad y comprobaciones

El ZIP original conserva el SHA-256:

```text
d4b4eae3c04395aa0c2aab2df6bde746978403c5229943f9ca8adda3deb741ed
```

El ejecutable conserva el SHA-256:

```text
2b9936b40e9a7676d38359db0921b96100ea892a61b32523de4bf40ff47358c9
```

Se han comprobado CRC de todos los archivos del ZIP, hashes y firma RSA del paquete de actualización. Las pruebas funcionales históricas se realizaron en Linux/Mono; sigue pendiente su validación en Windows nativo, especialmente el Programador de tareas y el arranque después de actualizar.

## Cuando llegue otra versión

Publicar el paquete comprobado, actualizar `app.json` y añadir sus notas arriba de [Notas de parche](../notas-de-parche/README.md). Mantener el historial anterior y comprobar la nueva descarga antes de sustituir archivos.
