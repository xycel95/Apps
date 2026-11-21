# Cómo funciona y guía

1. Descarga el programa, descomprime el ZIP si lo has elegido y abre
   `CorreoDiario.exe`. Puedes mover ese EXE a una carpeta compartida.
2. Elige una conexión:
   - **Personal · Outlook / Microsoft 365:** abre la ventana e inicia sesión
     en la página de Microsoft. Vuelve a CorreoDiario cuando veas tu bandeja.
   - **Trabajo · Outlook web:** permite indicar la dirección cuando la tengas.
   - **Outlook del equipo (clásico):** usa las cuentas ya configuradas en ese
     Outlook, sin necesitar conocer el enlace del trabajo.
3. Pulsa **Cargar carpetas**, marca las que quieras, elige la fecha y pulsa
   **Buscar correos**. Por defecto aparece el día de hoy.
4. Revisa los mensajes, elige el destino y pulsa **Guardar correos y adjuntos**.

Para la primera prueba conserva el borrado desmarcado y revisa los archivos.

## Archivos guardados

```text
2026-11-21_13.45_RECIBIDO_ana@ejemplo.es_CORREO.pdf
2026-11-21_13.45_RECIBIDO_ana@ejemplo.es_ADJ01_Solicitud.pdf
```

Se crea una carpeta nueva por ejecución. Si coinciden hora y remitente, añade
un número para evitar sobrescrituras. `Comprobacion.json` registra los archivos
comprobados y los movimientos confirmados.

## Borrado opcional

La casilla **Eliminar del buzón después de verificar la exportación** está
desmarcada al abrir y se desmarca después de cada operación.

Si la marcas, primero termina la exportación completa: abre los PDF, coteja su
texto, verifica los bytes guardados y comprueba otra vez el origen. Si falla
esta fase no comienza el movimiento de ningún mensaje. Solo después empieza
a mover los mensajes a **Eliminados**, sin vaciar esa carpeta.

Un corte de conexión durante los movimientos puede dejar algunos confirmados
y otro sin confirmar. El aviso y el registro lo indican; comprueba el buzón
antes de repetirlo. La aplicación no reintenta movimientos automáticamente.

## Sesiones y límites de la beta

Las sesiones web se guardan en el perfil de Windows del usuario, fuera de la
carpeta compartida. **Cerrar sesión** borra la sesión de esta aplicación.
**Desconectar** en modo clásico no cierra el Outlook que estabas utilizando.

El programa no incorpora un instalador de Edge WebView2. La conexión web puede
ser rechazada por ciertas versiones de Outlook o políticas del servidor.
Los PDF conservan el texto e imágenes adjuntas, con cambios posibles de estilo;
no son una réplica visual exacta. No se descargan imágenes externas. Ante
adjuntos inaccesibles o texto no verificable, conserva los mensajes originales.

La [validación de la publicación](VALIDACION-WINDOWS.json), cuando esté
disponible, distingue las pruebas automáticas, el arranque y lo que sigue
pendiente: inicio de sesión, impresión real y cuentas del usuario.
