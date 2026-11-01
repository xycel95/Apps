# Cómo funciona y guía

1. Descarga y extrae el paquete completo; abre `SSHBackup.exe`.
2. Añade tus servidores o importa la plantilla CSV/XLSX. Comprueba SSH y la huella del servidor.
3. Elige los servidores y pulsa **Iniciar copia**. El origen se utiliza solo para lectura.
4. Consulta el historial y la verificación del ZIP al terminar.

## Actualizar el programa

Desde 2.2, **Herramientas → Actualizaciones → Comprobar actualizaciones** utiliza el canal oficial preparado. Revisa las novedades firmadas y pulsa **Instalar actualización**. Muestra el progreso y permite cancelar antes de instalar; después reinicia el programa.

Para pasar desde 2.0/2.1, termina las copias, cierra el programa y sustituye `SSHBackup.exe` por el nuevo en su ubicación habitual. Usa el mismo usuario Windows. Este primer salto se hace manualmente porque el instalador de la entrega 2.1 tiene una incompatibilidad previa. Desde 2.2 las próximas versiones se instalan desde la app. Desde 1.x también se sustituye el EXE con el programa cerrado.

No debe haber copias en marcha. La configuración, los accesos, las huellas, el historial y el tema se conservan usando el mismo usuario de Windows y ubicación. Si mueves el EXE, vuelve a guardar la programación. Se guarda `.previous` y se restaura si no se puede iniciar el proceso nuevo.

Se mantienen **Abrir paquete**, el canal personalizado avanzado y **Usar canal oficial**. Hay accesos a las novedades de la web y a GitHub.

- [Guía completa vigente en texto](../Codigo/GUIA.txt)
- [Descargar](../descargas/README.md)
- [Notas de parche](../notas-de-parche/README.md)
- [Validación 2.2.0](VALIDACION-2.2.0.md)
- [Plantilla Excel](../plantillas/Plantilla_Servidores_SSH.xlsx)
- [PDF histórico de la guía 2.1.0](SSHBackup_Guia_de_uso.pdf)

Requiere Windows 10/11 y .NET Framework 4.8. No necesitas Python ni Excel para utilizarlo. La programación usa la hora del PC y requiere sesión iniciada; el buscador trabaja por nombres y rutas. Cada ZIP final es completo e independiente. Los ZIP originales del servidor no se descomprimen.
