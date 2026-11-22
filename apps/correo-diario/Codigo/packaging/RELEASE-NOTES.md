# CorreoDiario 0.4.8 beta

Reintentar solo los pendientes: conecta la misma cuenta y busca con las mismas
carpetas y fechas. Pulsa «Marcar pendientes» y elige el Comprobacion_….json del
lote anterior (compatible con 0.4.7). Exige copias verificadas, ausencia de
movimientos y que todos los IDs estén en la lista. No exporta ni borra por sí solo.
Tras un lote incompleto con copias verificadas, solo quedan marcados los
pendientes para el siguiente intento. El borrado queda desmarcado.

«Ver error» reabre el último aviso. Ante un fallo de asociación CID, la acción
separada «Copiar identificadores» copia las referencias fallidas y los Content-ID
de los adjuntos. Pueden contener direcciones internas: revisa el texto antes
de compartirlo. No incluye cuerpo, nombres de archivo ni datos de sesión.
«Copiar error» y el registro siguen excluyendo esos valores, que permanecen
solo durante la sesión. Esta versión recoge evidencia de las imágenes
pendientes; no cambia las reglas de asociación ni declara resuelto el CID.

Validación: 131 pruebas de lógica. Windows con casillas reales, portapapeles
Unicode, controles al tamaño mínimo, PDF WebView2 con imágenes, segunda lectura
de adjuntos y cero movimientos con pendientes. Datos sintéticos.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
