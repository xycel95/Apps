# Notas de parche

## 1.0.1 · 1 de noviembre de 2026

- Nombre definitivo **Guarda** en la ventana, el ejecutable, los metadatos, los informes y la documentación.
- Conserva las preferencias de Copia Fácil al abrir Guarda si todavía no hay preferencias propias.
- Recompilación y comprobación de una copia real desde la ventana en Linux/Mono.
- Publicación del ejecutable, paquete completo y código en la rama permanente `guarda` del repositorio Apps.

El algoritmo de copia se mantiene. Pendiente validar en Windows real y con un pendrive físico.

## 1.0.0 · 1 de noviembre de 2026

Primera entrega, llamada **Copia Fácil**:

- Selección de carpeta de origen y destino, incluidas unidades USB y otras carpetas accesibles.
- Copias completas con fecha `aaaa_mm_dd` al principio del nombre y numeración adicional cuando se repiten.
- Verificación SHA-256 activada por defecto, progreso y cancelación.
- Copias incompletas identificadas, detección de cambios en el origen y exclusión indicada de enlaces.
- Conservación de subcarpetas vacías y fechas cuando el destino lo permite.
- Informes y preferencias locales.
- 16 pruebas del motor superadas en Linux/Mono y revisión de la ventana con una copia real.

[Descripción del programa](../DESCRIPCION.md) · [Cómo funciona y guía](../docs/README.md) · [Descargar](../descargas/README.md).
