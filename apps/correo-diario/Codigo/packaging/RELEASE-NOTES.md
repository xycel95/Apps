# CorreoDiario 0.4.4 beta

Añade una segunda composición cuando el PDF con formato no supera el cotejo.
Usa texto sencillo, conserva la numeración de listas y reúne las imágenes al
final. Esa copia solo se acepta si toda la secuencia de letras y cifras coincide
en orden y cantidad. El PDF indica el formato y el aviso final cuenta las
copias simplificadas. No se reutiliza como válida la primera copia rechazada.

Si fallan ambos intentos, se detiene y registra el número de correo y recuentos
de ambos cotejos en PdfFailure, sin copiar palabras ni fragmentos del cuerpo
en ese diagnóstico. Mantiene destino directo, Desde/Hasta, segunda lectura de
adjuntos y SHA-256, y borrado desmarcado sin movimientos antes de verificar todo.

Validación: 88 pruebas y flujo Windows con WebView2 real. Incluye un rechazo
que podía persistir en 0.4.3, composición alternativa completa de varias páginas,
listas romanas/alfabéticas, imagen y adjunto leído dos veces, recibo y cero
movimientos. El caso concreto del usuario sigue pendiente de comprobación;
las pruebas de publicación utilizan correos sintéticos.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
