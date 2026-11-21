# CorreoDiario 0.4.3 beta

Corrige el falso aviso de texto incompleto cuando una palabra o dirección se
parte entre líneas o páginas del PDF. La aceptación de esos saltos exige que
todas las letras y cifras coincidan en orden y cantidad; se mantiene la
comprobación de palabras completas y se siguen rechazando omisiones.

Adapta tablas al ancho de página y retira alturas/recortes que pueden ocultar
texto al imprimir. Recoge el texto con el formato de impresión activado.
Mantiene todos los archivos juntos en el destino, Desde/Hasta y el borrado
opcional desmarcado, sin movimientos antes de verificar toda la selección.

Validación: 83 pruebas de lógica y regresión de Windows con WebView2 real,
palabras y direcciones largas, tabla ancha de 90 filas, varias páginas,
Unicode e imágenes CID. La regresión reproduce el falso rechazo del cotejo
anterior. Flujo con Edge y OWA sintético: cinco PDF, cinco adjuntos con
segunda lectura y SHA-256, cero movimientos. No se han probado los correos
concretos de la captura ni una cuenta real del usuario.

EXE portátil Windows x64; .NET incluido; necesita WebView2 Runtime para PDF.
