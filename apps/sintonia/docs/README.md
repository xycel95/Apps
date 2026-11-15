# Cómo funciona y guía

## Empezar

1. Descarga el programa desde [Descargar](../descargas/README.md).
2. Abre el ejecutable; no necesita archivos al lado ni instalar dependencias.
3. Selecciona un canal de la columna izquierda para reproducirlo.
4. Usa la estrella para guardarlo y la pestaña Favoritos para encontrarlo después.

## Añadir la lista de España de IPTV-org

1. Pulsa **Añadir lista**.
2. Selecciona **Lista M3U por enlace** y escribe un nombre.
3. En **Dirección o archivo**, pega:

```text
https://iptv-org.github.io/iptv/countries/es.m3u
```

4. Decide el alcance del filtro:

| Opción | Resultado |
| --- | --- |
| Filtro de emisores identificados activado | Selección inicial conservadora de orígenes; esta fuente ya viene incluida. |
| Filtro desactivado | Importa todos los enlaces compatibles de la lista, sin verificar su autorización o funcionamiento. |

5. Pulsa **Añadir**. Si la misma fuente y el mismo filtro ya existen, se indicará.

También puedes pegar `https://iptv-org.github.io/`: se utilizará la lista española.
El HTML de esa página no es un archivo M3U. El catálogo puede mostrar canales
que no tienen enlace de emisión y, por tanto, no se añaden al reproductor.

## Otras fuentes

- **Archivo M3U del equipo:** selecciona el tipo, pulsa Buscar y elige el archivo.
- **Emisión directa:** introduce el nombre y un enlace al vídeo, por ejemplo un
  directo `.m3u8`. Una dirección HLS de un solo canal se añade con esta opción.
- **Tus listas:** activa o desactiva fuentes, actualízalas o quita las añadidas.
  La selección inicial se puede desactivar y reactivar.

## Controles

| Acción | Control |
| --- | --- |
| Reproducir o pausar | Botón principal o Espacio |
| Detener | Botón cuadrado |
| Pantalla completa | F11; Esc para salir |
| Buscar | Ctrl+F |
| Actualizar listas | Ctrl+R |
| Canal anterior o siguiente | Ctrl+flecha izquierda/derecha |
| Silenciar | M |
| Otra señal o pista de audio | Desplegables inferiores, cuando hay alternativas |

Si una señal falla, la app prueba otra cuando existe y realiza un reintento
limitado. También puedes usar Reintentar o Web del emisor.

## Ajustes y diagnóstico

Los ajustes se conservan en `%LOCALAPPDATA%\XyceL\Sintonia\preferences.json`.
El motor está en el subdirectorio `runtime`; no necesita estar junto al EXE.
Los errores inesperados se anotan en `errors.log` en el mismo directorio.
No publiques esos ajustes o registros: las listas que añadas pueden contener
direcciones privadas o credenciales.

[Descripción del programa](../DESCRIPCION.md) · [Descargar](../descargas/README.md) ·
[Notas de parche](../notas-de-parche/README.md).
