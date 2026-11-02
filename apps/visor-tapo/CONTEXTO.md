# Contexto de Visor Tapo

## Estado

Versión 1.1.0. Antes: Visor C230. Aplicación portable Windows x64, local, una
cámara cada vez. Código Python/Qt, iniciador nativo C, RTSP para directo y
pytapo para consultar/descargar grabaciones microSD. Se descarga cada tramo
antes de reproducirlo; FFmpeg prepara y comprueba el MP4.

El usuario solicitó publicar en `xycel95/Apps` siguiendo la estructura de los
otros programas y disponer de una lista técnica de cámaras compatibles en
vez de clasificar modelos por pruebas realizadas/pendientes.

## Compatibilidad

Lista revisada el 02/11/2026: 133 modelos, kits y variantes comerciales de los
catálogos/centros de soporte global, español y estadounidense, más referencias
adicionales de las FAQ oficiales 4191 y 4372. 64 con RTSP documentado, 10 con
condiciones, 47 sin directo compatible y 12 sin información suficiente.
Las fichas y condiciones están en `docs/COMPATIBILIDAD.md` y el JSON de Codigo.
No es un inventario garantizado de todos los mercados históricos.

El acceso a microSD con pytapo es una previsión por integración para almacenamiento
en la propia cámara. No hay certificación del fabricante por modelo para esta app.
No se implementan cámaras hijas ni vídeos de un hub. Doble lente y 4G tienen límites.

## Evidencia y corrección importante

- El usuario confirmó imagen en directo y catálogo microSD de C230 en Windows
  con Visor C230 1.0.1. Su hardware/firmware no quedaron registrados.
- Se reprodujo y corrigió en 1.0.2 el bucle de eventos anidado al iniciar una descarga:
  `camera.getUserID()` debe ejecutarse antes de `asyncio.run(download_recording(...))`.
  Regresión comprobada con Downloader real y sesión de medios simulada.
- 17 pruebas automatizadas; conversión real con vídeo y audio sintéticos.
- Interfaz con dos pestañas, reproducción de 30 fotogramas de un MP4 sintético y
  búsqueda de C230 dentro de las 133 filas de compatibilidad, en Linux/offscreen.
- La descarga corregida con cámara física, audio real, giros y DPAPI nativo
  no tienen confirmación física registrada. No afirmar ensayos nativos de 1.1.0 en Windows.

## Desarrollo y entrega

Trabajar desde `Codigo/`: instalar `requirements-dev.txt`, ejecutar pruebas según
su README y generar paquetes con `python build_windows.py`. `build_inputs.json`
fija versiones y hashes de Python embebido, ruedas Windows y FFmpeg. El compilador
Zig genera el iniciador PE x64 con icono y manifiesto, sin elevación.

Las descargas y sus hashes se registran en app.json y descargas/SHA256SUMS.txt
cuando la publicación se haya comprobado. El ZIP completo usa los archivos de
una publicación de GitHub por su tamaño. No hay actualización automática en la app.
Conservar `%LOCALAPPDATA%\VisorC230`, claves protegidas y nombres antiguos de caché.

No se han publicado credenciales, vídeos privados ni capturas reales del usuario.
Las capturas de documentación se generaron con configuración vacía.

## Publicación comprobada

Publicado en [visor-tapo-v1.1.0](https://github.com/xycel95/Apps/releases/tag/visor-tapo-v1.1.0) desde la revisión
`522bd9cacb2ec76e06e005e9c8394cc61d77e93b`. El [flujo de compilación](https://github.com/xycel95/Apps/actions/runs/41495074415) ha terminado correctamente.
Los dos ZIP públicos se han descargado y validado; consulta
`docs/VALIDACION-PUBLICACION.json`, `app.json` y `descargas/SHA256SUMS.txt`.
El flujo inicial de una sola ejecución se ha retirado de la rama al terminar;
el historial y la etiqueta conservan sus instrucciones y resultados. La app
no incorpora actualizador ni programación de publicaciones.
