# Descripción del programa

**Bloques PDF** permite reunir muchos documentos PDF en unos pocos archivos para imprimirlos por lotes.

## Qué permite hacer

- Añadir varios PDF con el botón **Añadir PDF**.
- Arrastrar PDF o carpetas desde el Explorador de Windows.
- Elegir una carpeta para añadir todos sus PDF, con subcarpetas opcionales.
- Agruparlos de **50 en 50 por defecto**, con una cantidad configurable.
- Añadir documentos sin un límite fijo impuesto por la aplicación; la capacidad práctica depende de los archivos y del equipo.
- Elegir la carpeta de destino al pulsar **Crear bloques**.
- Generar **BLOQUE 1.pdf, BLOQUE 2.pdf…**, con numeración desde 1 en cada trabajo.
- Añadir cada documento completo una sola vez, sin repeticiones.
- Copiar los PDF originales en carpetas por bloques como alternativa a unirlos.
- Revisar la lista de documentos y los bloques previstos, quitar seleccionados, cancelar el proceso y abrir el resultado.
- Utilizar modo oscuro y consultar los errores dentro de la aplicación.
- Exportar únicamente los PDF, sin CSV, TXT ni archivo de control.

## Cómo se cuenta un bloque

Se cuentan **PDF originales**. Con 100 PDF de una página y bloques de 50 se generan **dos PDF de 50 páginas cada uno**. Cada documento aparece una sola vez. Las copias necesarias se eligen después al imprimir.

Los documentos de varias páginas se conservan completos. El programa no divide un documento para limitar las páginas del bloque.

## Requisitos y documentos firmados

Windows 10/11 con **.NET Framework 4.8 o posterior**. El programa funciona en el ordenador, sin cuenta ni conexión a Internet para procesar los documentos.

Los originales se abren para lectura y no se modifican. Unirlos produce una **copia para impresión**; la firma digital válida debe conservarse y comprobarse en los originales. El modo de carpetas copia los originales sin cambiar sus bytes.

La versión 1.0.4 se ha compilado y comprobado de forma nativa en Windows, incluida la ventana y 16 comprobaciones con documentos sintéticos. Microsoft Defender no detectó amenazas en el paquete. Se distribuyen el EXE normal y sus DLL por separado: extrae todo el ZIP y conserva sus componentes juntos. Quedan pendientes una impresora física y el antivirus del equipo del usuario. Consulta el [informe de comprobaciones](docs/VERIFICACION.txt).

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) · [Notas de parche](notas-de-parche/README.md).
