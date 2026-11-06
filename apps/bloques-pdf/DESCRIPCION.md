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
- Incluir una o dos copias completas y seguidas de cada documento; se proponen dos por defecto.
- Copiar los PDF originales en carpetas por bloques como alternativa a unirlos.
- Revisar la lista de documentos y los bloques previstos, quitar seleccionados, cancelar el proceso y abrir el resultado.
- Utilizar modo oscuro y consultar un listado CSV con la composición de cada bloque.

## Cómo se cuenta un bloque

Se cuentan **PDF originales**. Con 100 PDF de una página y bloques de 50:

| Copias de cada documento | Resultado |
| --- | --- |
| 1 | Dos PDF de 50 páginas cada uno. |
| 2, valor por defecto | Dos PDF de 100 páginas cada uno. |

Los documentos de varias páginas se conservan completos. El programa no divide un documento para limitar las páginas del bloque.

## Requisitos y documentos firmados

Windows 10/11 con **.NET Framework 4.8 o posterior**. El programa funciona en el ordenador, sin cuenta ni conexión a Internet para procesar los documentos.

Los originales se abren para lectura y no se modifican. Unirlos produce una **copia para impresión**; la firma digital válida debe conservarse y comprobarse en los originales. El modo de carpetas copia los originales sin cambiar sus bytes.

La versión distribuida se ha compilado para Windows y su motor se ha comprobado con datos sintéticos. Queda pendiente comprobar la ventana en Windows, una impresora física y el antivirus del equipo. Consulta el [informe de comprobaciones](docs/VERIFICACION.txt).

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) · [Notas de parche](notas-de-parche/README.md).
