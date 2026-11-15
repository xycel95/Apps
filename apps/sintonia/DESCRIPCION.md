# Descripción del programa

**Sintonía** reproduce televisión en directo desde listas M3U y enlaces de emisión
autorizados. Funciona en Windows 10/11 x64 y reúne el reproductor y sus dependencias
en un único ejecutable: no necesitas instalar VLC ni .NET.

## Qué permite hacer

- Consultar una selección de España procedente de IPTV-org, actualizable al abrir.
- Buscar canales sin distinguir tildes, filtrar categorías y guardar favoritos.
- Añadir listas por enlace HTTP/HTTPS o archivos M3U/M3U8, y emisiones directas.
- Agrupar las señales alternativas de un canal, elegir audio y usar pantalla completa.
- Conservar el catálogo anterior si una actualización falla.

La selección inicial contiene **49 canales** en la instantánea del 15/11/2026.
Su tamaño puede cambiar al actualizar. Incluye, por ejemplo, 24 Horas,
Telemadrid, Canal Sur Andalucía y canales autonómicos y locales.

## Fuentes y disponibilidad

El catálogo general de IPTV-org contiene canales sin enlace de emisión y su lista
comunitaria puede incluir enlaces que no estén autorizados. La selección inicial
aplica un filtro conservador de procedencia; **no es una certificación jurídica**.
No se incluyen credenciales de servicios ni mecanismos para evitar DRM,
suscripciones o restricciones geográficas.

Las listas importadas deben contener emisiones que tengas derecho a utilizar.
Algunos canales o programas pueden no estar disponibles por horario o derechos.

## Datos

Listas, favoritos y volumen se guardan en `%LOCALAPPDATA%\XyceL\Sintonia`.
Mover el ejecutable conserva estos ajustes en el mismo equipo y usuario.
El motor de vídeo se prepara automáticamente en ese directorio la primera vez.
No hay cuentas ni analítica; las conexiones se realizan con las fuentes y emisores.

[Cómo funciona y guía](docs/README.md) · [Descargar](descargas/README.md) ·
[Notas de parche](notas-de-parche/README.md).
