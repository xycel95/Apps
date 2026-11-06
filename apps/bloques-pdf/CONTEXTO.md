# Estado de Bloques PDF

- Aplicación: **Bloques PDF**, versión **1.0.1**, publicada inicialmente el 6 de noviembre de 2026.
- Repositorio público: `xycel95/Apps`; rama permanente: `bloques-pdf`; carpeta: `apps/bloques-pdf`.
- Paquete: `descargas/BloquesPDF_1.0.1_Windows.zip`, el mismo entregado al usuario, con ejecutable, fuentes y documentación.
- SHA-256 del ZIP: `5dc51887db90ba072feb10315097eeb3afdd7cac0e2e8185d74a9158a06493fe`.
- SHA-256 de `BloquesPDF.exe`: `c441de86c1fd5160e5405149c4204f802bde2bd8eda0d0edf1d20545a64565e4`.

## Requisitos acordados

Selección múltiple con botón, arrastre de PDF o carpetas y selección de carpeta completa. No hay límite fijo de entrada. Se proponen 50 PDF originales por bloque y se pregunta el destino al crear los bloques. Los nombres son BLOQUE 1.pdf, BLOQUE 2.pdf, etc., desde 1 en cada trabajo.

Actualmente se incluyen **dos copias por documento por defecto**. Se puede elegir una; no confundir documentos con páginas ni volver a duplicar al imprimir. Los originales se conservan intactos; los PDF unidos son copias para impresión. El modo de carpetas conserva exactamente los bytes de los originales.

## Código y compilación

C# con Windows Forms y .NET Framework 4.8, compilado con SDK .NET 10. PDFsharp 6.2.4 importa las páginas. El proyecto `Codigo/build` integra las dependencias administradas utilizadas en un EXE sin compresión ni ofuscación.

Consulta `Codigo/COMPILAR.txt`. El ZIP conserva la entrega original; `Codigo/` reproduce su carpeta `CodigoFuente/`.

## Evidencia y pendientes

27 aserciones del motor superadas el 6 de noviembre de 2026, incluyendo 3.000 PDF en 60 bloques. El mismo EXE distribuido procesó 100 originales en dos bloques y 202 páginas desde un host .NET 10, porque uno de los originales sintéticos tenía dos páginas y se usaron dos copias.

Se verificaron los nombres BLOQUE 1.pdf a BLOQUE 60.pdf y las referencias del CSV. Las comprobaciones visuales de firma y formulario se realizaron en 1.0.0; el cambio 1.0.1 afecta a los nombres y al versionado. `docs/VERIFICACION.txt` detalla el alcance.

Pendientes: arranque e interfaz en Windows real, antivirus corporativo e impresora física. No hay firma Authenticode ni actualizador automático configurado.

Los ensayos de `Codigo/tests/Program.cs` requieren PDF sintéticos en carpetas `input/` y `special/`, no incluidos. No publicar certificados privados de ensayo, documentos del trabajo ni carpetas generadas por las pruebas.
