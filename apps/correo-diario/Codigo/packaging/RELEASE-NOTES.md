# CorreoDiario 0.3.1 beta

Corrige el error 400 al buscar mensajes por fecha. El filtro utiliza ahora
la envoltura y el nombre de constante que exige el contrato JSON de OWA.
Mantiene la conexión con la pestaña abierta y la sesión del navegador.

La consulta usa resúmenes de destinatarios y obtiene los datos completos al
exportar. Se limpia la lista anterior antes de buscar y los avisos identifican
la operación que falla.

Validación: 59 pruebas; formatos antiguos rechazados con 400 por un lector
independiente del contrato; Edge real y servicio HTTPS sintético con búsquedas
por fecha y carpeta, paginación, cuatro PDF del renderizador WebView2 real y
cuatro adjuntos originales. Cero movimientos con el borrado desmarcado.
Pendiente confirmar la exportación con las cuentas reales del usuario.

Descarga el EXE o el ZIP para Windows x64. .NET incluido; necesita WebView2
Runtime para los PDF. La casilla de borrado sigue opcional y desmarcada. Solo
mueve a Eliminados tras verificar toda la exportación y nunca reintenta movimientos.
