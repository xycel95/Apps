# Comprobaciones de SSH Backup 2.2.0

## Linux/Mono

90 comprobaciones correctas: 14 del actualizador, 6 de su interfaz, 17 de importación/configuración, 34 de selección y migración de días y 19 de herramientas/regresión.

Se han comprobado la clave RSA original, el paquete firmado, la detección de notas/EXE alterados, HTTPS, cancelación previa a conectar, canal oficial para ajustes vacíos, conservación de canales personalizados, sustitución y restauración del EXE y rechazo de reinstalaciones. Interfaz revisada visualmente en claro y oscuro, incluyendo tamaño mínimo.

Compilación con Roslyn, versión de ensamblado 2.2.0.0 y manifiesto nativo de Windows extraído del PE y comparado con su fuente. Las dependencias y sus licencias se conservan.

## Windows

Comprobación correcta en Windows Server 2025 / .NET Framework 4.8 mediante [GitHub Actions](https://github.com/xycel95/Apps/actions/runs/41266607684): compilación independiente, 14 pruebas del actualizador, 6 de interfaz y ensayo real de preparado/confirmación/salida/reemplazo/reinicio con el paquete firmado. El EXE anterior queda conservado. La entrega se compila con las referencias oficiales de Framework 4.8, no con las DLL de ejecución de Mono.

Se ha detectado que Quote del cliente original 2.1 falla por una sobrecarga de String no disponible en Framework 4.8. Para este primer salto se recomienda cerrar el programa y sustituir el EXE, conservando ubicación y usuario. A partir de 2.2 el canal y el instalador quedan preparados.

El flujo de la rama permanente añade dos pruebas contra el canal HTTPS público y su descarga real con progreso, firma y hash. Consultar su resultado para esa revisión publicada.

## Límites

No se ha ejecutado el Programador de tareas de Windows ni utilizado servidores reales del destinatario en esta entrega. El motor SFTP no ha cambiado; se conserva la validación histórica con su alcance original. La restauración cubre el fallo al lanzar el proceso, no una prueba completa de todas las funciones de la nueva versión.
