# Cómo funciona y guía

## Exportar carpetas de Outlook web

1. Abre **CorreoDiario-Edge.exe** y pulsa **Exportar carpetas de Outlook…**. Si Informática ya te dio un archivo de configuración, pulsa **Configuración…** si los campos están ocultos y **Abrir configuración…**. Si te dio dos identificadores, introdúcelos en esos campos.
2. Pulsa **Conectar con Microsoft 365**. Elige la cuenta de trabajo en el navegador y completa la identificación que pida la organización. Volverás a CorreoDiario con la lista de carpetas del buzón.
3. Marca todas las carpetas que quieras exportar. Para incluir una subcarpeta, márcala también. El programa recuerda tu elección al exportar. Elige el destino en Windows y pulsa **Exportar carpetas**. Puedes minimizar la app, cambiar de pestaña o cerrar Edge después de identificarte. Mantén CorreoDiario abierto.
4. Pulsa **Abrir resultado** al terminar. Dentro de `CorreoDiario_365` encontrarás una carpeta por correo con `Correo.pdf` y sus adjuntos. Si marcas varias carpetas, el informe conserva el origen de cada mensaje. `Hilos` reúne los mensajes del mismo hilo incluidos en el lote.

## Primera conexión en un equipo de trabajo

Pulsa **Copiar solicitud para Informática** si aún no tienes una configuración. El botón prepara el texto para que puedas enviarlo tú. Informática puede registrar o habilitar un cliente público de escritorio en Microsoft Entra, asignar el acceso que corresponda a tu cuenta, y confirmar el destino permitido para el archivo. La aplicación pide el permiso delegado de Microsoft Graph `Mail.Read`, que permite leer tu buzón. La selección de carpetas se aplica en CorreoDiario. El servicio de identidad gestiona la identificación y el consentimiento en el navegador predeterminado.

Configura la plataforma **Aplicaciones móviles y de escritorio**, con la redirección `http://localhost`. Facilita al usuario el ID de aplicación y el ID de directorio, sobre todo si el registro es de un único inquilino. También se pueden entregar como `CorreoDiario.M365.json` junto al ejecutable usando los nombres de campos siguientes.

```json
{
  "ClientId": "<ID de aplicación>",
  "TenantId": "<ID de directorio>"
}
```

Si el acceso requiere aprobación o la política del dispositivo impide la conexión, **Copiar diagnóstico** recoge la fase y el código disponibles para Informática. El mensaje de solicitud contiene los datos del registro necesarios. Introduce tus credenciales únicamente en la página de Microsoft o de tu organización que abra el navegador.

[Registro de aplicaciones de escritorio](https://learn.microsoft.com/entra/identity-platform/scenario-desktop-app-registration) · [Permiso delegado de lectura de correo](https://learn.microsoft.com/graph/permissions-reference#mailread)

## Resultado, reintentos y espacio del buzón

El programa lee el cuerpo y los adjuntos en memoria. Comprueba el PDF generado y las huellas de los archivos escritos. Un adjunto que ya se llame `Correo.pdf` recibe un nombre independiente. Al repetir el lote con la misma cuenta y destino, se comprueban las copias previas. Una copia que hayas cambiado se conserva y se genera otra verificada.

`Ultimo-lote.txt` muestra cuántos mensajes se verificaron y cuántos quedan pendientes. Su informe detallado, en `_registro/Lotes`, identifica el correo afectado. Una carpeta que no se pudo leer, un vínculo a un archivo, un mensaje protegido o un archivo sin verificar quedan señalados. Puedes corregir el problema y volver a pulsar **Exportar carpetas**. El listado corresponde a los mensajes encontrados durante esa ejecución. Los mensajes que lleguen después se recogen al repetirla.

La exportación conserva los mensajes en el buzón. El espacio ocupado allí solo cambia si después se retiran mensajes según el procedimiento de tu organización. El diagnóstico que se copia incluye fases, errores y contadores. Los detalles del contenido se quedan en los archivos locales del lote.

## Archivos existentes

La ventana principal también permite añadir mensajes EML o MSG existentes y recibir una entrega completa de Windows. **Detectar Edge** y **Ver selección en Edge** muestran los contadores visibles. Cuando el portapapeles solo ofrece texto o enlaces, usa **Exportar carpetas de Outlook…** para acceder a los mensajes completos mediante Microsoft 365.

### Comprobaciones de la versión

La suite utiliza correo de ejemplo y un servicio de Microsoft 365 simulado. El acceso a una cuenta corporativa real requiere la configuración de su organización.
