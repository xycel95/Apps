# Informes y correcciones de compatibilidad

Los informes deben distinguir funciones verificadas y pendientes.

1. Indica modelo exacto, revisión de hardware y firmware desde la información
   del dispositivo en Tapo. Añade versión de Visor Tapo y de Windows.
2. Prueba por separado la detección, el directo, el catálogo microSD, la apertura
   de un vídeo y los giros si tu cámara los admite.
3. Describe cada resultado: «funciona», «falla» o «no probado». Ver una lista de
   vídeos no confirma que se hayan podido descargar y reproducir.
4. Si falla una operación, exporta su diagnóstico desde el programa.

No incluyas contraseñas, correo de la cuenta, números de serie, MAC, archivos de
ajustes, vídeos privados ni capturas con las claves visibles.

`src/compatibility.json` alimenta la lista de la aplicación. Al editarlo,
actualiza también `COMPATIBILIDAD.md` y enlaza la ficha o nota de firmware.
La lista indica compatibilidad técnica según protocolos, almacenamiento y
límites de esta versión. No utiliza «probado/pendiente» como clasificación.

«Previsto con pytapo» es una inferencia de integración, no una certificación
de descarga por modelo. Conserva esa distinción al añadir cámaras. Las pruebas
realizadas y su alcance se registran en `VALIDACION.txt`; ver el catálogo no
demuestra que una grabación pueda descargarse.

Comunica el informe en [Issues de Apps](https://github.com/xycel95/Apps/issues/new),
con «Visor Tapo» en el título y los datos anteriores. Se incluye también una
plantilla YAML en `.github/ISSUE_TEMPLATE/compatibilidad.yml` para repositorios
que quieran habilitarla en su rama predeterminada.
