# Continuar Bloques PDF

Lee primero `/AGENTS.md`, este archivo y `CONTEXTO.md`. Trabaja en la rama permanente `bloques-pdf` y conserva la estructura `apps/bloques-pdf/`.

- Mantén los cuatro accesos públicos en este orden: Descripción del programa, Cómo funciona y guía, Descargar, Notas de parche.
- Mantén los 50 PDF originales por bloque y los nombres BLOQUE 1.pdf, BLOQUE 2.pdf, etc., salvo petición del usuario.
- Cada documento se añade una sola vez. No reincorpores controles de copias ni repeticiones internas.
- La exportación solo contiene PDF: no escribas informes TXT/CSV ni archivos de control. Los errores se muestran en la aplicación. La búsqueda recursiva debe seguir excluyendo los resultados generados sin necesitar un marcador nuevo.
- La selección admite botón de varios archivos, arrastre y carpeta completa. Se pregunta el destino al crear los bloques.
- Las carpetas usan BloquesPDF_yyyy.MM.dd_HH.mm.ss, sin identificador aleatorio. Si coinciden el segundo y el destino, añade (2), (3), etc.; no sobrescribas trabajos anteriores y conserva el reconocimiento de los formatos antiguos en el escaneo.
- Los originales son de solo lectura. Los bloques unidos son copias para impresión; el modo de carpetas conserva los bytes originales.
- Lee `Codigo/COMPILAR.txt` antes de compilar. El SDK de desarrollo es .NET 10; el ejecutable final usa .NET Framework 4.8 y Windows Forms.
- Desde 1.0.4 se distribuye el EXE normal del SDK con sus DLL y su .config. No fusiones bibliotecas con ILRepack ni uses ofuscación o empaquetadores autoextraíbles. Mantén icono, manifiesto y tipo GUI.
- Para publicar una nueva compilación, comprueba su arranque nativo y el paquete con Microsoft Defender mediante el flujo de Windows; conserva el informe y las huellas de los bytes publicados. No desactives el antivirus ni añadas exclusiones para realizar la comprobación.
- Verifica nombres, páginas completas sin duplicados, exportación solo con PDF, agrupación, cancelación y correspondencia entre ejecutable y fuentes al cambiar el código. Reutiliza la validación si solo publicas los mismos bytes.
- Los ensayos necesitan PDF sintéticos no distribuidos. No publiques `tests/work`, certificados privados, claves, documentos del trabajo, salidas de pruebas ni archivos `bin/obj`.
- Coordina versión, paquete, SHA-256, `app.json`, guía y notas de parche. Añade notas nuevas arriba sin borrar las anteriores.
- No afirmes ensayos de Windows, antivirus o impresora física sin evidencia. No configures actualizaciones automáticas ni modifiques la web por publicar en GitHub.
