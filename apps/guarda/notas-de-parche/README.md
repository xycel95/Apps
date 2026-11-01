# Notas de parche

## 1.1.1 · 1 de noviembre de 2026

- Compilación de distribución con Microsoft Roslyn en Windows, con los recursos y el manifiesto incorporados por el compilador en una sola pasada.
- Compilación de desarrollo con Roslyn; Mono queda como herramienta de ejecución de pruebas en Linux.
- Metadatos de editor coherentes con XYCEL. Se mantiene la clave pública del actualizador.
- Pruebas nativas de copias, temas, verificación y actualización entre procesos, incluida la sustitución y apertura reales del ejecutable.
- Análisis del archivo con Microsoft Defender activo e informe con hash, herramientas y resultado. No se detectan amenazas en ese análisis; no se garantiza la aceptación en todos los antivirus o equipos.
- Documentación que diferencia la firma RSA del canal de actualizaciones y un certificado Authenticode del ejecutable.
- Corrección de las pruebas gráficas para ejecutarse también en Windows y registro del número real de casos ejecutados en cada plataforma.
- Prueba de la ventana con el bucle de mensajes de Windows, para esperar correctamente la finalización de las operaciones asíncronas. Captura completa de errores y códigos de salida de los procesos de prueba.
- Análisis obligatorio con la herramienta oficial MpCmdRun y el servicio Defender activo. Los fallos del proveedor de consulta CIM se registran por separado; no sustituyen ni omiten el análisis del ejecutable.

Se conserva el diseño y la funcionalidad de 1.1.0. El motivo exacto del bloqueo mostrado en el equipo del usuario sigue pendiente de identificar: Defender tampoco informa de amenazas en el archivo anterior de la prueba.

## 1.1.0 · 1 de noviembre de 2026

- Nueva interfaz con tarjetas, selección por pasos, vista previa del nombre y progreso separado.
- Modo oscuro predeterminado y tema claro; la elección se conserva.
- Botón **Actualizaciones**, búsqueda manual de versiones y novedades.
- Descarga e instalación desde la app con reinicio y conservación de preferencias.
- Canal oficial firmado RSA/SHA-256 y comprobación de longitud, huella, identidad y versión del ejecutable.
- Preparación del instalador antes de cerrar, espera a la salida de Guarda y copia del ejecutable anterior. Restauración si falla la apertura de la nueva versión.
- No se permite abrir el actualizador durante una copia.
- Documentación, imágenes, metadatos, descargas y canal actualizados.
- 16 pruebas del motor y 13 del actualizador en Linux/Mono; revisión de ambos temas y una copia real desde la ventana.

El proceso completo de actualización en un Windows real sigue pendiente de validación. Desde 1.0.1 hay que descargar esta versión manualmente una vez para disponer del botón integrado.

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
