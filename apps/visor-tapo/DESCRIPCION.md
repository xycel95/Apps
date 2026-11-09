# Descripción del programa

**Visor Tapo** permite utilizar una cámara Tapo desde un ordenador Windows
conectado a la misma red local. Puedes ver el directo y consultar por fecha
las grabaciones almacenadas en su propia tarjeta microSD.

## Qué permite hacer

- Buscar cámaras en casa sin conocer previamente su IP.
- Consultar dónde se configuran el usuario y las contraseñas de acceso.
- Abrir el directo con sonido, elegir calidad, pausar, capturar y usar pantalla completa.
- Consultar los tramos grabados, descargar el seleccionado para reproducirlo
  y continuar con el siguiente. Los tramos largos se dividen hasta cinco minutos.
- Guardar una copia MP4 y gestionar las copias locales desde el Explorador.
- Realizar giros cortos en modelos motorizados compatibles.
- Buscar un modelo en la lista de compatibilidad y exportar diagnósticos sin contraseñas.

## Requisitos y modelos

- Windows 10/11 de **64 bits**. Un único ejecutable portable incluye Python, Qt y FFmpeg.
- PC y cámara accesibles en la misma red local privada IPv4.
- Para el directo: cámara con RTSP y una «Cuenta de cámara» creada en Tapo.
- Para grabaciones: microSD en la propia cámara, vídeos y acceso local compatible
  mediante pytapo con las credenciales de Tapo necesarias.

La [lista de compatibilidad](docs/COMPATIBILIDAD.md) revisa **133 modelos, kits y
variantes** con enlaces a sus fichas. La previsión de acceso a microSD depende
del hardware y firmware. Una cámara de batería puede carecer de directo RTSP;
un modelo de doble objetivo o 4G puede tener condiciones adicionales.

## Uso y datos

Descarga el `.exe` y ábrelo directamente. Se puede mover o renombrar. Los archivos de funcionamiento se preparan en una carpeta temporal y se limpian al cerrar normalmente; no necesitas conservar carpetas junto al ejecutable.

Gestiona una cámara cada vez. La grabación seleccionada se descarga antes de
reproducirse y su copia permanece en el PC. El guardado de contraseñas es opcional
y está protegido por el usuario de Windows mediante DPAPI.

No borra ni formatea la microSD, no configura su programación de grabación y
no accede a Tapo Care, cámaras hijas ni vídeos almacenados detrás de un hub.
El uso inicial está pensado para la red de casa.

Antes se llamaba **Visor C230**. Mantiene sus ajustes y copias locales.
Proyecto independiente, sin afiliación con TP-Link ni Tapo.

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) · [Notas de parche](notas-de-parche/README.md).
