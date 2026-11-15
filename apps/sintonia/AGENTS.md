# Trabajar en Sintonía

Aplicar el AGENTS.md de la raíz y responder en español.

- Rama permanente: `sintonia`. Carpeta: `apps/sintonia/`.
- Leer CONTEXTO.md, app.json, notas de parche y revisión remota antes de editar.
- Conservar los cuatro apartados principales y el código en Codigo/.
- `main` solo recibe la entrada del catálogo; no fusionar la rama de la aplicación.
- Reproductor local Windows x64 con .NET 10, WinForms y LibVLC 3.0.24.
- No incluir ajustes reales, logs, listas privadas, tokens, grabaciones ni credenciales.
- El usuario ha pedido que arranque vacío. No reintroducir listas, canales ni
  catálogos precargados. El filtro de procedencia es opcional y viene desmarcado.
- Al actualizar, retirar solo las antiguas fuentes automáticas; preservar las
  listas importadas por el usuario, sus favoritos y volumen.
- No presentar fuentes como legales o disponibles sin evidencia ni añadir bypass de DRM o suscripciones.
- Mantener caché, favoritos y volumen en el directorio histórico del usuario.
- No añadir binarios grandes al historial. El ejecutable y los archivos completos
  se entregan como assets de una publicación; conservar licencias y fuentes.
- Preparar LibVLC con Codigo/packaging/Prepare-Runtime.ps1 y compilar con build.ps1.
- Reutilizar pruebas vigentes cuando no cambian los bytes; distinguir importación,
  arranque nativo y reproducción real. No afirmar pruebas de Windows o directos
  que no se hayan realizado.
- Coordinar versión, hash, enlaces, manifiesto y notas. Preservar las notas anteriores.
- La publicación de una versión puede usar un flujo de una sola ejecución para compilar,
  comprobar y cargar los archivos. Retirar el disparador al terminar; no crear
  tareas programadas ni configuración de actualizador por esta publicación.
