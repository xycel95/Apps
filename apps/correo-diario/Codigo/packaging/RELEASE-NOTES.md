# CorreoDiario 0.4.2 beta

Guarda todos los PDF y adjuntos directamente en la carpeta elegida. Ya no
crea una subcarpeta por intento. Si coincide un nombre, añade un número al
PDF y sus adjuntos y conserva lo anterior. Cada ejecución tiene su propio
registro Comprobacion_fecha_hora_id.json en ese mismo destino.

Mantiene Desde/Hasta, la verificación de contenido con segunda lectura y
SHA-256, y el borrado opcional desmarcado. No reintenta movimientos.

Validación: 75 pruebas, incluyendo ejecuciones sucesivas y simultáneas sin
sobrescrituras. Flujo Windows con Edge, servicio OWA sintético, cinco PDF
reales de WebView2 y cinco adjuntos directamente en la carpeta elegida,
sin subcarpetas y con cero movimientos.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
