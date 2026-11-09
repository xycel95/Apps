# Descargar

**Visor Tapo 1.2.0** · Windows 10/11 de **64 bits**.

## Programa

**[Descargar Visor Tapo — un único .exe](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.2.0/Visor_Tapo_1.2.0_Windows.exe)** · 144.9 MiB.

1. Descarga el ejecutable.
2. Ábrelo directamente. Puedes moverlo o renombrarlo.

El archivo incluye el programa, Python, Qt y FFmpeg. Al abrirlo prepara sus
archivos temporales automáticamente y, al cerrar normalmente, los retira.
Durante la preparación aparece una barra de progreso.

Los ajustes, claves protegidas y vídeos siguen guardados en el perfil de Windows
(`%LOCALAPPDATA%\VisorC230`). Se conservan al mover el ejecutable dentro del mismo
PC y usuario. Esos datos personales no van incluidos en el `.exe`.

Para actualizar, cierra Visor C230 o Visor Tapo y abre este nuevo ejecutable.
Consulta la [guía de conexión](../docs/README.md) y la
[lista de compatibilidad por modelo](../docs/COMPATIBILIDAD.md).

## Comprobaciones de esta entrega

Compilado y ejecutado en Windows. Se ha abierto desde una carpeta vacía,
tras moverlo y renombrarlo, y con apertura y cierre normales. También se han
comprobado reproducción con vídeo sintético, proceso aislado de grabaciones,
credenciales DPAPI y limpieza de temporales. Han pasado **25 pruebas automatizadas**.

[Validación nativa](../docs/VALIDACION-WINDOWS-1.2.0.json) ·
[Comprobación de los archivos publicados](../docs/VALIDACION-PUBLICACION-1.2.0.json) ·
[Compilación y pruebas](https://github.com/xycel95/Apps/actions/runs/42597795470). Estas comprobaciones no conectan
cámaras físicas ni amplían la compatibilidad por modelos.

Huellas: [SHA256SUMS.txt](SHA256SUMS.txt) y
[archivo de integridad de la publicación](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.2.0/SHA256SUMS.txt).

## Código y archivo histórico

[Código fuente en GitHub](../Codigo/) · [ZIP de código para desarrollo](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.2.0/Visor_Tapo_Codigo_1.2.0.zip) ·
[Publicación 1.2.0](https://github.com/xycel95/Apps/releases/tag/visor-tapo-v1.2.0) ·
[Paquete anterior 1.1.0](https://github.com/xycel95/Apps/releases/tag/visor-tapo-v1.1.0).

[Descripción del programa](../DESCRIPCION.md) · [Cómo funciona y guía](../docs/README.md) · [Notas de parche](../notas-de-parche/README.md).
