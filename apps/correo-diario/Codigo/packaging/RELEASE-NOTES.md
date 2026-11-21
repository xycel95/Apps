# CorreoDiario 0.4.5 beta

Añade Copiar error en los avisos y Copiar último error en la ventana principal.
El informe sigue disponible durante la sesión aunque cierres el aviso o borres
el archivo de registro. El texto también se puede seleccionar y copiar a mano.

Incluye versión, mensaje, progreso y los diagnósticos de ambos intentos del PDF.
Añade códigos Unicode del primer carácter diferente, categoría y tipo de cambio,
sin copiar palabras ni fragmentos del cuerpo ni volcar las cabeceras de sesión.
No se transmite nada automáticamente.

Validación: 92 pruebas, informe disponible tras borrar el registro y pulsación
del botón real con comprobación del portapapeles Unicode en Windows. Conserva
el flujo con Edge/OWA sintético, PDF e imágenes reales y segunda lectura de
adjuntos. El fallo concreto de un carácter sigue pendiente de diagnóstico:
esta versión no cambia las reglas de aceptación ni permite borrar ante un fallo.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
