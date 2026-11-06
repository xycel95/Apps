# Estado de Bloques PDF

- Versión actual: **1.0.3**, 6 de noviembre de 2026.
- Repositorio público: `xycel95/Apps`; rama permanente: `bloques-pdf`; carpeta: `apps/bloques-pdf`.
- Paquete: `descargas/BloquesPDF_1.0.3_Windows.zip`; incluye ejecutable, fuentes y documentación.
- SHA-256 del ZIP: `f51e23176da0d8c9f7d18227d922984dff77dcecb3f462d1ba7259e1db2bab4d`.
- SHA-256 de `BloquesPDF.exe`: `c24fb6adfcf87885c39a38e422660fbd2d6ddde36aa410e9c791dbc9aea6b3a0`.

## Requisitos acordados

Selección múltiple con botón, arrastre de PDF o carpetas y selección de carpeta completa. No hay límite fijo de entrada. Se proponen 50 PDF originales por bloque y se pregunta el destino al crear los bloques. Los nombres son BLOQUE 1.pdf, BLOQUE 2.pdf, etc., desde 1 en cada trabajo.

Por petición expresa del usuario, 1.0.2 elimina la opción de copias: **cada documento se añade una sola vez**. La exportación contiene **solo PDF**, sin CSV, TXT ni archivo de control. Las copias necesarias se eligen en la impresora. Los errores se muestran completos en la aplicación, con desplazamiento, sin informes en el destino.

Los originales se conservan intactos. El modo de carpetas conserva exactamente sus bytes. Los PDF unidos son copias para impresión; la firma digital válida debe comprobarse en los originales.

## Código y compilación

C# con Windows Forms y .NET Framework 4.8, compilado con SDK .NET 10. PDFsharp 6.2.4 importa las páginas. `Codigo/build` integra las dependencias administradas utilizadas en un EXE sin compresión ni ofuscación. Consulta `Codigo/COMPILAR.txt`.

Las carpetas usan el formato BloquesPDF_yyyy.MM.dd_HH.mm.ss, sin identificador aleatorio. Si coinciden el destino y el segundo, se añade (2), (3), etc. La reserva utiliza un archivo de bloqueo temporal fuera del destino que se elimina al cerrar; no añade archivos a la exportación. Los resultados nuevos y antiguos se excluyen del escaneo recursivo por el patrón de su carpeta generada. Se reconoce también el marcador de versiones antiguas para no reimportar sus bloques; la versión nueva no escribe marcadores.

## Evidencia y pendientes

33 aserciones del motor superadas, incluyendo 3.000 PDF en 60 bloques y 3.000 páginas, sin ningún archivo adicional. El EXE distribuido, cargado desde un host .NET 10, procesó 100 originales de una página en exactamente dos PDF con 100 páginas en total; también se verificaron originales de varias páginas sin duplicados.

`docs/VERIFICACION.txt` registra el alcance actual. Se conserva la evidencia de 1.0.1 y 1.0.2 en los informes con sus sufijos. `docs/PUBLICACION.json` corresponde a la publicación actual.

Dos procesos del EXE actualizado crearon cuatro trabajos distintos en el mismo segundo. Se verificaron el formato con puntos, los sufijos de colisión, la ausencia de archivos extra y las páginas conservadas. La comprobación pública del paquete actual se registra en docs/PUBLICACION.json.

Pendientes: interfaz y nuevo diálogo de errores en Windows real, antivirus corporativo e impresora física. No hay firma Authenticode ni actualizador automático.

Las pruebas de `Codigo/tests/Program.cs` requieren PDF sintéticos en carpetas `input/` y `special/`, no incluidos. No publicar certificados privados, documentos del trabajo ni salidas de ensayo.
