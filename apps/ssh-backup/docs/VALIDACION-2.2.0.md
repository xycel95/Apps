# Comprobaciones de SSH Backup 2.2.0

## Linux/Mono

90 comprobaciones correctas: 14 del actualizador, 6 de su interfaz, 17 de importación/configuración, 34 de selección y migración de días y 19 de herramientas/regresión.

Se han comprobado la clave RSA original, el paquete firmado, la detección de notas/EXE alterados, HTTPS, cancelación previa a conectar, canal oficial para ajustes vacíos, conservación de canales personalizados, sustitución y restauración del EXE y rechazo de reinstalaciones. Interfaz revisada visualmente en claro y oscuro, incluyendo tamaño mínimo.

Compilación con Roslyn, versión de ensamblado 2.2.0.0 y manifiesto nativo de Windows extraído del PE y comparado con su fuente. Las dependencias y sus licencias se conservan.

## Windows

El flujo `SSH Backup - actualizaciones Windows` compila las fuentes, prueba firma/sustitución/restauración e interfaz, y ensaya el saludo del instalador, el reemplazo y el reinicio de la entrega firmada mediante un host ficticio. Consultar el resultado real de Actions; este informe no declara una ejecución nativa hasta que exista ese resultado.

## Límites

No se ha ejecutado el Programador de tareas de Windows ni utilizado servidores reales del destinatario en esta entrega. El motor SFTP no ha cambiado; se conserva la validación histórica con su alcance original. La restauración cubre el fallo al lanzar el proceso, no una prueba completa de todas las funciones de la nueva versión.
