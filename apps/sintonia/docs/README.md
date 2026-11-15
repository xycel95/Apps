# Cómo funciona y guía

## Primera apertura

Sintonía viene vacía: no incluye listas, canales ni una selección automática.
Pulsa **Añadir lista** para incorporar tus propias fuentes.

## Añadir una lista por enlace

1. Pulsa **Añadir lista**.
2. Selecciona **Lista M3U por enlace**.
3. Escribe un nombre y pega la dirección HTTP/HTTPS de la lista.
4. Pulsa **Añadir**.

El filtro de emisores identificados de España es opcional y viene desmarcado.
Solo se aplica si lo activas expresamente. Una dirección terminada en .m3u8
puede ser una lista de canales o una emisión: elige el tipo según su contenido.

## Añadir un archivo o una emisión

- **Archivo M3U del equipo:** selecciona el tipo, pulsa Buscar y elige el archivo.
- **Emisión directa:** escribe el nombre y el enlace directo al vídeo del canal.
- **Tus listas:** activa, desactiva o quita tus fuentes y consulta sus actualizaciones.

Los nombres del catálogo no garantizan una señal disponible. Una página web de
RTVE Play, Atresplayer o Mediaset Infinity no se puede pegar como emisión directa.

## Actualizar desde 1.0.0

Cierra la versión anterior y abre el nuevo ejecutable. Solo se retira la antigua
fuente automática de España; no se borran tus listas importadas ni tus favoritos.
Si no habías añadido ninguna fuente, el programa aparecerá vacío.

## Controles

| Acción | Control |
| --- | --- |
| Reproducir o pausar | Botón principal o Espacio |
| Detener | Botón cuadrado |
| Pantalla completa | F11; Esc para salir |
| Buscar | Ctrl+F |
| Actualizar tus listas | Ctrl+R |
| Canal anterior o siguiente | Ctrl+flecha izquierda/derecha |
| Silenciar | M |
| Otra señal o audio | Desplegables inferiores, si hay alternativas |

Si no encuentras un canal importado, selecciona Todos y Todas las categorías.
Si una señal falla, puedes reintentar o abrir Web del emisor cuando esté disponible.

Los ajustes se conservan en `%LOCALAPPDATA%\XyceL\Sintonia\preferences.json`.
No publiques preferencias o registros: tus fuentes pueden contener datos privados.

[Descripción del programa](../DESCRIPCION.md) · [Descargar](../descargas/README.md) ·
[Notas de parche](../notas-de-parche/README.md).
