# Estado de Bloques PDF

- Versión actual: **1.0.2**, 6 de noviembre de 2026.
- Repositorio público: `xycel95/Apps`; rama permanente: `bloques-pdf`; carpeta: `apps/bloques-pdf`.
- Paquete: `descargas/BloquesPDF_1.0.2_Windows.zip`; incluye ejecutable, fuentes y documentación.
- SHA-256 del ZIP: `83785db1177f89f0eb53c8561de61442c6e15806e72f972dc2e99c68b4ff11af`.
- SHA-256 de `BloquesPDF.exe`: `4584561e5d1678841dc4e5f95efaa1ef0da3eebf559b897ca8538bfee4556d4e`.

## Requisitos acordados

Selección múltiple con botón, arrastre de PDF o carpetas y selección de carpeta completa. No hay límite fijo de entrada. Se proponen 50 PDF originales por bloque y se pregunta el destino al crear los bloques. Los nombres son BLOQUE 1.pdf, BLOQUE 2.pdf, etc., desde 1 en cada trabajo.

Por petición expresa del usuario, 1.0.2 elimina la opción de copias: **cada documento se añade una sola vez**. La exportación contiene **solo PDF**, sin CSV, TXT ni archivo de control. Las copias necesarias se eligen en la impresora. Los errores se muestran completos en la aplicación, con desplazamiento, sin informes en el destino.

Los originales se conservan intactos. El modo de carpetas conserva exactamente sus bytes. Los PDF unidos son copias para impresión; la firma digital válida debe comprobarse en los originales.

## Código y compilación

C# con Windows Forms y .NET Framework 4.8, compilado con SDK .NET 10. PDFsharp 6.2.4 importa las páginas. `Codigo/build` integra las dependencias administradas utilizadas en un EXE sin compresión ni ofuscación. Consulta `Codigo/COMPILAR.txt`.

Los resultados se excluyen del escaneo recursivo por el patrón de su carpeta generada. Se reconoce también el marcador de versiones antiguas para no reimportar sus bloques; la versión nueva no escribe marcadores.

## Evidencia y pendientes

33 aserciones del motor superadas, incluyendo 3.000 PDF en 60 bloques y 3.000 páginas, sin ningún archivo adicional. El EXE distribuido, cargado desde un host .NET 10, procesó 100 originales de una página en exactamente dos PDF con 100 páginas en total; también se verificaron originales de varias páginas sin duplicados.

`docs/VERIFICACION.txt` registra el alcance actual. Se conserva la evidencia de 1.0.1 en los informes con ese sufijo. `docs/PUBLICACION.json` corresponde a la publicación actual.

La descarga pública de 1.0.2 respondió HTTP 200 y coincidió con el tamaño y el SHA-256 del paquete. También se comprobaron su CRC, la huella del ejecutable y todas las fuentes incluidas.

Pendientes: interfaz y nuevo diálogo de errores en Windows real, antivirus corporativo e impresora física. No hay firma Authenticode ni actualizador automático.

Las pruebas de `Codigo/tests/Program.cs` requieren PDF sintéticos en carpetas `input/` y `special/`, no incluidos. No publicar certificados privados, documentos del trabajo ni salidas de ensayo.
