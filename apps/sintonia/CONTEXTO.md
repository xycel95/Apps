# Contexto de Sintonía

El usuario solicitó una app de Windows para ver IPTV y añadir la lista española
de IPTV-org. Se creó Sintonía 1.0.0 y autorizó publicarla en xycel95/Apps.

## Decisiones

- .NET 10 y WinForms, vídeo mediante LibVLCSharp 3.10.2 y LibVLC 3.0.24 x64.
- Un único EXE autónomo; el motor se extrae automáticamente al perfil del usuario.
- La lista de España completa contenía 338 señales en la instantánea inicial,
  incluidas etiquetas de canales de suscripción. El filtro inicial conserva
  49 señales de emisores identificados. Es selección de origen, no certificación.
- El usuario puede importar una lista completa desmarcando explícitamente el filtro.
- No hay servidor intermediario ni servicio de actualizaciones configurado.

## Estado

Sintonía 1.0.0 publicada en GitHub, etiqueta `sintonia-v1.0.0`.
Código compilado: `66744e5c30fd6e7171ab078fee9111c3d32d795f`.

Las 26 comprobaciones pasan en Windows. Se han confirmado ventana e inicio de
LibVLC, apertura desde carpeta vacía y tras mover/renombrar el EXE. Se han
verificado por descarga los cinco assets publicados, sus tamaños y SHA-256,
la integridad de los ZIP y el EXE común al archivo suelto y al ZIP.

La reproducción de directos dentro del EXE sigue pendiente de validación.
Las consultas HLS y el fragmento analizado en Linux son evidencia de fuentes,
no de reproducción integral del ejecutable. Consultar los informes de docs/.

La comprobación de traslado espera hasta 20 segundos la liberación del archivo
al cerrar; la apertura original ya había pasado antes de añadir esa espera.
El flujo temporal de publicación se retira una vez completado y no queda un
servicio ni automatismo recurrente. Las preferencias y datos reales no se suben.

## Organización

Codigo/ contiene el código, los recursos pequeños, las pruebas y la preparación
reproducible del motor. Las bibliotecas y sus archivos de fuentes se obtienen de
los proyectos originales y se incluyen en las descargas, fuera del historial Git.
No se han incorporado datos de uso, preferencias, registros o credenciales.
