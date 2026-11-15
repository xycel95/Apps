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

Las 26 comprobaciones de importación, persistencia, filtrado y fallos HTTP pasan
en Linux. La compilación Windows x64 se completó sin errores ni advertencias.
Cinco señales consultadas devolvieron HLS y un fragmento de Telemadrid fue H.264.
No se pudo ejecutar el EXE completo desde Linux. La publicación inicial incorpora
una comprobación del arranque del EXE en Windows; consultar el informe final
en docs/VALIDACION-WINDOWS.json y el estado de app.json antes de afirmarla.

## Organización

Codigo/ contiene el código, los recursos pequeños, las pruebas y la preparación
reproducible del motor. Las bibliotecas y sus archivos de fuentes se obtienen de
los proyectos originales y se incluyen en las descargas, fuera del historial Git.
No se han incorporado datos de uso, preferencias, registros o credenciales.
