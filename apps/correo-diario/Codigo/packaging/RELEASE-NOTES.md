# CorreoDiario 0.4.6 beta

Corrige el falso aviso de texto incompleto identificado por el diagnóstico:
el selector U+FE0F de presentación emoji se omitía al extraer el texto del PDF.
Ahora el cotejo excluye únicamente FE0E/FE0F, sin cambiar el HTML ni las imágenes.
Sigue exigiendo las letras, cifras, acentos y otras marcas. El registro cuenta
los selectores de presentación en el texto esperado y el extraído.

Validación: 100 pruebas. Cuatro regresiones fallaban antes de corregir la
normalización; se mantienen negativos de letras, cifras, acentos y otros
selectores. PDF reales de WebView2 en Windows con FE0E/FE0F y cifra cambiada
rechazada. Conserva Copiar error/Copiar último error, fechas y destino directo,
segunda lectura/SHA-256 de adjuntos y verificación global antes de movimientos.

Corregido el mecanismo identificado; pendiente confirmar la ejecución completa
con la cuenta real. Las pruebas de publicación utilizan correos sintéticos.
EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
