# Descripción del programa

**Sintonía** reproduce televisión por Internet desde las fuentes que tú añadas.
Funciona en Windows 10/11 x64 en un único ejecutable con .NET y LibVLC incluidos.

## Arranque vacío

Desde la versión 1.0.1 no se incluye ninguna lista ni catálogo de canales.
La primera apertura muestra cero canales y el botón **Añadir lista**.
No se descarga ninguna lista hasta que tú añadas una fuente.

Si actualizas desde 1.0.0, se retira únicamente la antigua lista automática.
Las listas que importaste, sus canales guardados, favoritos y volumen se conservan.

## Funciones

- Importar listas M3U/M3U8 por enlace HTTP/HTTPS o archivo local.
- Añadir una emisión directa, buscar canales y filtrar categorías.
- Guardar favoritos y agrupar las señales alternativas de cada canal.
- Pantalla completa, volumen, pausa y selección de audio cuando esté disponible.
- Conservar la última lista válida si una actualización falla.

No hay cuentas ni analítica. Los ajustes se guardan en
`%LOCALAPPDATA%\XyceL\Sintonia`; se conservan al mover el ejecutable.

La disponibilidad y los derechos dependen de las fuentes que añadas. La aplicación
no certifica las emisiones ni evita DRM, suscripciones o restricciones geográficas.
Las páginas web de los reproductores oficiales no son enlaces IPTV directos.

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) ·
[Notas de parche](notas-de-parche/README.md).
