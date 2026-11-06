# Notas de parche

## 1.0.3 · 6 de noviembre de 2026

- Las carpetas de resultados usan el formato **BloquesPDF_2026.11.06_21.09.19**, con fecha y hora separadas por puntos.
- Se elimina el identificador aleatorio del nombre de la carpeta.
- Si se crean varios trabajos en el mismo segundo, se añade ` (2)`, ` (3)`, etc., para conservarlos sin sobrescribir.
- La búsqueda recursiva reconoce las carpetas nuevas y las de versiones anteriores.

## 1.0.2 · 6 de noviembre de 2026

- Eliminada la opción «Copias de cada documento». Cada documento se añade una sola vez.
- La exportación contiene únicamente los PDF; se dejan de generar `detalle_documentos.csv`, `LEEME_RESULTADO.txt` y `BloquesPDF.trabajo`.
- Los errores se muestran completos dentro de la aplicación, en una ventana con desplazamiento.
- La búsqueda recursiva sigue excluyendo los resultados generados sin crear archivos de control.
- Se mantienen los 50 originales por bloque, los nombres BLOQUE 1.pdf, BLOQUE 2.pdf, etc., y la elección del destino.

## 1.0.1 · 6 de noviembre de 2026

- Los PDF generados se llaman **BLOQUE 1.pdf, BLOQUE 2.pdf, BLOQUE 3.pdf…**.
- El modo de carpetas utiliza **BLOQUE 1**, **BLOQUE 2**, etc.
- Cada trabajo comienza su numeración en 1 dentro de una carpeta nueva.
- Actualizadas la documentación y las comprobaciones de los nombres.

## 1.0.0 · 5 de noviembre de 2026

- Primera versión de Bloques PDF para Windows.
- Añadir varios PDF, arrastrar archivos o carpetas y elegir una carpeta completa.
- Bloques de 50 PDF originales por defecto, con cantidad configurable.
- Elección del destino al crear los bloques.
- Una o dos copias completas por documento, con dos por defecto.
- Modo alternativo para copiar los originales firmados en carpetas.
- Listas de documentos y bloques previstos, orden natural, progreso, cancelación y modo oscuro.
- Comprobación previa de archivos, detección de cambios por SHA-256 e informes TXT/CSV.

[Descripción del programa](../DESCRIPCION.md) · [Cómo funciona y guía](../docs/README.md) · [Descargar](../descargas/README.md).
