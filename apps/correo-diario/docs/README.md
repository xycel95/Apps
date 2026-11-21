# Cómo funciona y guía

## Conectar tu pestaña de Outlook

1. Abre tu correo en tu **Edge o Chrome habitual**, con tu sesión ya iniciada.
2. Ejecuta `CorreoDiario.exe` y elige ese navegador.
3. Pulsa **Cómo conectar**. Abre la dirección que muestra la guía:
   `edge://inspect/#remote-debugging` en Edge o
   `chrome://inspect/#remote-debugging` en Chrome.
4. En **Remote debugging / Depuración remota**, activa **Allow remote debugging
   for this browser instance** (permitir la conexión para esta instancia).
5. Vuelve a CorreoDiario y pulsa **Buscar pestañas**. El navegador pedirá
   permiso: pulsa **Permitir** si quieres conectar el programa.
6. Elige tu pestaña de Outlook y pulsa **Usar esta pestaña**.
7. Si pide renovar la conexión, pulsa **Actualizar** en Outlook y vuelve a
   **Usar esta pestaña**. Mantén seleccionada la misma pestaña.

La cuenta personal y la laboral se eligen por su pestaña; no necesitas introducir
su dirección de acceso en el programa. El inicio de sesión, si hiciera falta,
se realiza siempre en el navegador habitual.

El permiso técnico del navegador da acceso a su sesión, aunque CorreoDiario
opera sobre la pestaña seleccionada. **Desconectar** termina la conexión sin
cerrar el navegador ni cerrar tu cuenta. Las cabeceras de autorización de las
solicitudes correctas de Outlook se conservan solo en memoria y se descartan al
desconectar. Puedes desactivar también la opción
del navegador al terminar. La aplicación no cambia esa opción automáticamente.

Si la opción no aparece o está bloqueada por la organización, esta conexión no
está disponible en ese equipo. Esta beta usa las carpetas estándar de Edge y
Chrome; no detecta perfiles portátiles ni rutas personalizadas.

## Guardar correos y adjuntos

Marca carpetas (incluido Enviados), elige **Desde** y **Hasta** y pulsa
**Buscar correos**. Ambos días se incluyen completos. Para buscar un único día,
pon la misma fecha en los dos campos. **Hoy** pone ambos en el día actual.
La lista muestra fecha y hora; al cambiar las fechas se limpia la búsqueda anterior.
Después,
revisa los mensajes y selecciona el destino. Después pulsa **Guardar correos
 y adjuntos**. Para la primera prueba, deja el borrado desmarcado.

```text
2026-11-21_13.45_RECIBIDO_ana@ejemplo.es_CORREO.pdf
2026-11-21_13.45_RECIBIDO_ana@ejemplo.es_ADJ01_Solicitud.pdf
```

Todos los PDF y adjuntos quedan **directamente en la carpeta que has elegido**.
No se crean subcarpetas por intento. Si ya hay archivos con esos nombres,
el programa añade un número al grupo de correo y adjuntos y conserva lo anterior.
Cada ejecución guarda su propio registro `Comprobacion_….json` en ese destino.

## Borrado opcional

La casilla está desmarcada por defecto. Solo al marcarla se moverán mensajes a
**Eliminados**, tras completar la exportación de todos los seleccionados,
abrir y cotejar los PDF, verificar los archivos y volver a comprobar el origen.
Cada adjunto se lee dos veces y se comparan su longitud y SHA-256. El tamaño
del objeto en Outlook puede incluir propiedades y se registra aparte de los
bytes reales del archivo; no se usa como una longitud exacta.
El registro `Comprobacion_….json` conserva ambos tamaños y la verificación de la segunda lectura.
Si falla esa fase no comienza el movimiento de ningún correo.

No se vacía Eliminados. Esa carpeta puede seguir ocupando cuota del buzón.
Si se pierde la conexión durante los movimientos, puede haber algunos
confirmados y otro sin confirmar; el aviso y el registro lo indican. Comprueba
el buzón antes de repetirlo. No se reintentan movimientos automáticamente.

## Outlook clásico y límites

El modo **Outlook del equipo (clásico)** conserva el acceso al perfil local
configurado; no controla el nuevo Outlook. No necesita una conexión del navegador.

El componente WebView2 del equipo se utiliza solo para componer el PDF. Los PDF
no son una réplica visual exacta, las imágenes externas no se descargan y algunos
formatos, adjuntos enlazados o protegidos no pueden verificarse en esta beta.
Consulta la [validación de Windows](VALIDACION-WINDOWS.json) y la
[verificación de la descarga](VALIDACION-PUBLICACION.json) para distinguir las
pruebas simuladas de la conexión e impresión con tus cuentas reales.
