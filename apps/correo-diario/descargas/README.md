# Descargar

**CorreoDiario 0.2 beta · Windows de 64 bits.**

- [Descargar el ejecutable](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.2.0-beta.1/CorreoDiario_0.2_beta.exe)
- [Descargar el ZIP con el ejecutable, guía y licencias](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.2.0-beta.1/CorreoDiario_0.2_beta.zip)
- [Descargar el código fuente](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.2.0-beta.1/CorreoDiario_0.2_beta_fuentes.zip)
- [Comprobar SHA-256](https://github.com/xycel95/Apps/releases/download/correo-diario-v0.2.0-beta.1/SHA256SUMS.txt)
- [Ver la publicación](https://github.com/xycel95/Apps/releases/tag/correo-diario-v0.2.0-beta.1)

Si eliges el ZIP, descomprímelo y abre `CorreoDiario.exe`. Puedes mover ese
EXE a una carpeta compartida. Requiere Microsoft Edge WebView2 Runtime en
el equipo; .NET está incluido y no se instala una extensión en el navegador.

Esta versión usa la pestaña de Outlook que ya tienes abierta en Edge o Chrome.
Pulsa **Cómo conectar** para ver la opción del navegador que debes activar y
acepta su aviso de conexión. Después selecciona tu pestaña. No hay un formulario
de inicio de sesión dentro de CorreoDiario ni se instala una extensión.
El modo clásico sigue disponible para perfiles configurados en Outlook clásico.

**Validación:** 35 pruebas automáticas, conexión WebSocket local simulada,
arranque del EXE, selector de pestañas y borrado desmarcado comprobados en
Windows. Quedan pendientes el permiso y la conexión con tu navegador real,
la impresión y las cuentas personales/laborales. [Informe](../docs/VALIDACION-WINDOWS.json).

El borrado es opcional y mueve a Eliminados después de verificar toda la
exportación. No vacía esa carpeta; puede seguir ocupando cuota.
