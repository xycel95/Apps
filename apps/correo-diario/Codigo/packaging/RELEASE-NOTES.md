# CorreoDiario 0.4 beta

Corrige la petición de descarga de adjuntos que omitía BaseShape, obligatorio
en AttachmentShape y causante del 400 del formato anterior.

Añade Desde y Hasta: ambos días completos incluidos. Para un solo día, pon
la misma fecha en los dos campos. Hoy pone ambos en el día actual. Funciona
con la conexión web y con Outlook clásico. La lista muestra fecha y hora,
ordena cronológicamente y se limpia cuando cambias las fechas.

Validación: 65 pruebas; lector independiente de contratos de búsqueda y adjuntos;
Edge real y servicio HTTPS sintético, intervalo de dos días, cinco PDF del
renderizador WebView2 real y cinco adjuntos originales. Borrado desmarcado,
cero movimientos. Pendiente confirmar la exportación con las cuentas reales.

Windows x64, .NET incluido; necesita WebView2 Runtime para los PDF. Conserva
la sesión del navegador abierto. El borrado sigue opcional y desmarcado; solo
mueve a Eliminados tras verificar todo, sin reintentos de movimientos.
