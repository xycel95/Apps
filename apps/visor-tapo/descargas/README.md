# Descargar

**Visor Tapo 1.1.0** · Windows 10/11 de **64 bits**.

- **[Descargar Visor Tapo para Windows](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.1.0/Visor_Tapo_1.1.0_Windows.zip)** — paquete portable completo, 137.6 MiB.
- [Descargar el código fuente](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.1.0/Visor_Tapo_Codigo_1.1.0.zip) · [Explorar el código](../Codigo/).
- [Ver la publicación de esta versión](https://github.com/xycel95/Apps/releases/tag/visor-tapo-v1.1.0).

## Abrir o actualizar

1. Descarga el ZIP de Windows y **extrae todo su contenido**.
2. Abre **`Visor Tapo.exe`** dentro de la carpeta extraída.
3. Conserva junto al ejecutable las carpetas **`runtime`**, **`src`** y **`bin`**.
4. Pulsa **«Buscar cámaras en casa»** y sigue la [guía de conexión](../docs/README.md).

No necesitas instalar Python, VLC ni FFmpeg. El `.exe` utiliza los componentes
del paquete y no se distribuye como ejecutable independiente.

Para actualizar desde Visor C230, cierra la versión anterior y abre el nuevo
paquete extraído. Conserva ajustes y copias en `%LOCALAPPDATA%\VisorC230`.

## Cámaras compatibles

Consulta la **[lista de 133 modelos, kits y variantes](../docs/COMPATIBILIDAD.md)**
antes de descargar. El directo requiere RTSP. El acceso a la microSD mediante
pytapo es una previsión de integración sujeta al hardware, firmware y credenciales;
no se certifica por el hecho de que el directo funcione. No hay acceso a vídeos
de Tapo Care, cámaras hijas ni grabaciones detrás de un hub.

## Integridad y comprobaciones

Las huellas están en [SHA256SUMS.txt](SHA256SUMS.txt) y en el
[archivo de integridad de la publicación](https://github.com/xycel95/Apps/releases/download/visor-tapo-v1.1.0/SHA256SUMS.txt).

Se han descargado los dos ZIP públicos y comprobado su SHA-256, CRC, contenido
y correspondencia de las fuentes. El paquete de Windows incluye un manifiesto
de archivos con sus huellas. La [compilación y comprobaciones](https://github.com/xycel95/Apps/actions/runs/41495074415) han
superado 17 pruebas y la reproducción de un vídeo sintético de 30 fotogramas
en Linux/Qt offscreen. Es una compilación cruzada para Windows; no se presenta
como ensayo nativo del paquete ni de todas las cámaras.

[Informe de publicación](../docs/VALIDACION-PUBLICACION.json) ·
[Alcance de la validación](../docs/VALIDACION.txt) · [Licencias de componentes](../docs/TERCEROS.md).

El ZIP completo se aloja en una publicación de GitHub por su tamaño y se enlaza
desde este apartado para mantener la estructura común de Apps.

[Descripción del programa](../DESCRIPCION.md) · [Cómo funciona y guía](../docs/README.md) · [Notas de parche](../notas-de-parche/README.md).
