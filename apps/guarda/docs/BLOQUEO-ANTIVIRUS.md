# Si el navegador o antivirus bloquea Guarda

Una captura con «se ha detectado un virus» confirma un bloqueo, pero no identifica por sí sola el motor, la amenaza, el archivo exacto ni la causa. No debe confundirse con una advertencia de editor desconocido.

## Qué se ha comprobado en 1.1.1

- Compilación de distribución con Microsoft Roslyn en Windows, sin empaquetadores, ofuscación ni modificación manual posterior de los recursos PE.
- Pruebas de copia, temas e instalación de actualizaciones con salida, sustitución y reapertura reales.
- Análisis del ejecutable con Microsoft Defender activo. El resultado, hash, tamaño, versión del antivirus y del compilador están en [VALIDACION-WINDOWS.json](VALIDACION-WINDOWS.json).
- Defender tampoco informa de amenazas al analizar el archivo anterior 1.1.0 de esa prueba. Esto no determina la causa de la detección del equipo del usuario ni constituye una resolución de Microsoft sobre un falso positivo.

## Certificados: dos firmas distintas

El canal de actualizaciones utiliza una firma RSA que permite al cliente comprobar que el anuncio lo ha firmado la clave de Guarda. **Eso no es una firma Authenticode del ejecutable.**

El `.exe` no dispone de un certificado de editor emitido por una autoridad reconocida. Firmarlo con un certificado de confianza permite identificar al editor, pero no garantiza que un archivo vaya a ser aceptado por todos los sistemas de protección. No se ha añadido un certificado autofirmado para aparentar esa confianza.

## Si tu equipo lo sigue bloqueando

1. Conserva el bloqueo. En **Seguridad de Windows → Protección contra virus y amenazas → Historial de protección**, abre la detección y anota su nombre exacto. Si utilizas otro antivirus, consulta su historial.
2. Comprueba qué versión y enlace estabas descargando. Las huellas publicadas permiten identificar el archivo exacto; no lo ejecutes si no coincide.
3. Facilita el nombre de la detección para investigarla. Sin ese dato no se puede confirmar si es un falso positivo, un archivo distinto o una política específica del equipo.
4. Si procede solicitar revisión a Microsoft, su portal exige una sesión de Microsoft. No se afirma que se haya enviado una muestra o que Microsoft haya retirado una detección.

No se propone desactivar el antivirus, añadir exclusiones ni cambiar o comprimir el archivo para ocultarlo al análisis. Los ZIP publicados son paquetes de distribución normales y también pueden analizarse.

Referencias oficiales:

- [Historial de protección](https://support.microsoft.com/en-us/windows/security/windows-security/protection-history-in-the-windows-security-app).
- [Envío de archivos para revisión](https://learn.microsoft.com/defender-xdr/submission-guide).
- [Preguntas para desarrolladores y firma de confianza](https://learn.microsoft.com/en-us/defender-xdr/developer-faq).
