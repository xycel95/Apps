# Continuar Bloques PDF

Lee primero `/AGENTS.md`, este archivo y `CONTEXTO.md`. Trabaja en la rama permanente `bloques-pdf` y conserva la estructura `apps/bloques-pdf/`.

- Mantén los cuatro accesos públicos en este orden: Descripción del programa, Cómo funciona y guía, Descargar, Notas de parche.
- No cambies los 50 PDF originales por bloque, las dos copias iniciales ni los nombres BLOQUE 1.pdf, BLOQUE 2.pdf, etc., salvo petición del usuario.
- La selección admite botón de varios archivos, arrastre y carpeta completa. Se pregunta el destino al crear los bloques.
- Los originales son de solo lectura. Los bloques unidos son copias para impresión; el modo de carpetas conserva los bytes originales.
- Lee `Codigo/COMPILAR.txt` antes de compilar. El SDK de desarrollo es .NET 10; el ejecutable final usa .NET Framework 4.8 y Windows Forms.
- El programa de integración de `Codigo/build` debe conservar el icono, el manifiesto, el tipo GUI y las dependencias necesarias. No uses compresión, ofuscación ni empaquetadores autoextraíbles.
- Verifica nombres, copias completas, agrupación y correspondencia entre ejecutable y fuentes al cambiar el código. Reutiliza la validación si solo publicas los mismos bytes.
- Los ensayos necesitan PDF sintéticos no distribuidos. No publiques `tests/work`, certificados privados, claves, documentos del trabajo, salidas de pruebas ni archivos `bin/obj`.
- Coordina versión, paquete, SHA-256, `app.json`, guía y notas de parche. Añade notas nuevas arriba sin borrar las anteriores.
- No afirmes ensayos de Windows, antivirus o impresora física sin evidencia. No configures actualizaciones automáticas ni modifiques la web por publicar en GitHub.
