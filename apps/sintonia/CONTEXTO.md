# Contexto de Sintonía

El usuario ha pedido explícitamente que Sintonía venga vacía y permita añadir
sus propias fuentes. Esta instrucción sustituye la selección inicial de España.

## Versión 1.0.1

- Sin fuentes ni canales por defecto y sin el recurso spain.m3u en el ejecutable.
- Esquema de preferencias 2. La migración retira únicamente BuiltIn=true o el
  ID histórico spain-official, sin borrar listas añadidas ni favoritos o volumen.
- Importaciones con filtro opcional desmarcado, sin activación por proveedor.
- No se descargan listas al abrir una instalación sin fuentes.
- Mismo reproductor, ruta de preferencias y dependencias que 1.0.0.

## Publicación y validación

Versión 1.0.1 publicada, etiqueta `sintonia-v1.0.1`.
Código compilado: `6dd7c2e0e5f2b2d1166bd0e1f2a8afc7f6b87828`.

30 comprobaciones pasan en Windows. El ensayo del EXE usa datos aislados y
confirma arranque con cero fuentes/canales, retirada de la fuente automática,
conservación de listas propias, favoritos y volumen, ventana/VLC y traslado.
Los cinco assets se han descargado y comprobado por tamaño, SHA-256 e integridad.
Consultar los informes actuales de docs/ y el manifiesto app.json.

El flujo temporal se retira tras publicar; no queda una tarea recurrente.
No se ha ampliado la compatibilidad con reproductores web ni se ha validado
la reproducción de todas las emisiones añadidas por el usuario.
