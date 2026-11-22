# CorreoDiario 0.4.7 beta

Aprovecha ContentId/ContentType completos de GetAttachment para enlazar imágenes
cuando faltan en el resumen del mensaje. Normaliza los URL CID con una sola
decodificación, espacios/corchetes y signo más. No adivina por nombre ni escoge
entre contenidos distintos con un identificador ambiguo.

Si una imagen sigue sin recuperarse o no se decodifica, ese correo queda
pendiente y continúa con los demás. Verifica todas las copias guardadas y bloquea
el borrado global si hay pendientes. No crea una copia válida con imágenes omitidas.
El aviso y Copiar error incluyen números pendientes e ImageFailure con motivos
sin referencias privadas. Los metadatos se cotejan también en la segunda lectura.

Validación: 119 pruebas. Windows con el renderizador real: dos PDF con imágenes,
identidad completa de descarga, referencias codificadas y adjuntos releídos;
un CID inexistente y una imagen dañada quedan pendientes. Cero movimientos
incluso con borrado marcado. La imagen real reportada sigue por comprobar.
Mantiene FE0E/FE0F, fechas, destino directo, Copiar error y archivos sin sobrescritura.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
