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

Compilar y publicar esta versión, completar la validación nativa de arranque
vacío y de migración aislada, verificar las descargas y retirar el flujo temporal.
No se está ampliando la compatibilidad con los reproductores web oficiales.
