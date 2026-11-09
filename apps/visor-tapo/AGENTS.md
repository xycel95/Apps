# Trabajar en Visor Tapo

Aplicar las instrucciones generales del `AGENTS.md` de la raíz. Responder en español.

## Organización

- Rama permanente: `visor-tapo`. Carpeta: `apps/visor-tapo/`.
- Leer `CONTEXTO.md`, `app.json`, notas de parche y revisión remota antes de continuar.
- Código en `Codigo/`, guía y compatibilidad en `docs/`, descargas en `descargas/`.
- Conservar los cuatro accesos principales: Descripción del programa, Cómo funciona y guía,
  Descargar y Notas de parche. `main` solo recibe la entrada del catálogo general.
- Conservar otras aplicaciones y sus ramas. No fusionar esta rama en `main`.

## Comportamiento y privacidad

- Aplicación local para Windows x64; una cámara cada vez, sin conexión automática al abrir.
- Directo RTSP con QtMultimedia; microSD mediante pytapo y descarga antes de reproducir.
- No borrar ni formatear tarjetas, configurar grabación, cámaras hijas, Tapo Care ni abrir puertos.
- Mantener el directorio histórico `%LOCALAPPDATA%\VisorC230` y la lectura de copias antiguas.
- Mantener claves por entrada privada al proceso y guardado opcional DPAPI, nunca texto plano.
- No incluir credenciales, ajustes reales, diagnósticos privados ni vídeos/capturas de cámaras.
  Las imágenes públicas muestran configuración vacía.

## Compatibilidad y validación

- La lista es documental y por función; no clasificar por probado/pendiente.
- `Codigo/src/compatibility.json` y `docs/COMPATIBILIDAD.md` deben coincidir.
  También se incluye la copia de COMPATIBILIDAD.md en Codigo y en los paquetes.
- «Compatible» en directo requiere RTSP documentado. Las etiquetas «Se aplica a»
  de una FAQ no bastan para atribuir todas sus funciones a cada modelo.
- «Previsto con pytapo» es una inferencia de integración para microSD en la propia cámara;
  no presentarlo como certificación ni afirmar descargas físicas sin evidencia.
- Conservar los límites de batería, hub, doble lente y red 4G. Enlazar la ficha y hardware.
- Reutilizar las pruebas vigentes si el código no cambia. Comprobar el flujo afectado,
  el ZIP, manifiesto de archivos y dependencias fijadas antes de sustituir una descarga.
- Registrar evidencia física y simulada por separado en `docs/VALIDACION.txt`.

## Publicación

- Coordinar versión en core, recursos, manifiesto Windows, build_inputs, app.json,
  paquetes, SHA-256 y notas. Las notas nuevas se añaden arriba.
- Desde 1.2.0 compilar en Windows x64 con `Codigo/build_onefile.py`. `build_windows.py` prepara el paquete interno; no es la descarga principal. Conservar hashes y licencias de dependencias.
- Entregar un único `.exe` portable desde una publicación de GitHub, enlazado en Descargas y app.json. No añadir binarios grandes al historial ni claves de acceso.
- Iniciador en `Codigo/portable_launcher.py`: preparar/verificar en temporales, esperar al proceso hijo y limpiar después de cerrarlo. Mantener aislado el modo de pruebas.
- Validar el EXE real con `Codigo/tests/validate_onefile_windows.py`: carpeta vacía, mover/renombrar, reproducción y worker, DPAPI, conservación de ajustes y cierre normal.
- Mantener los datos en `%LOCALAPPDATA%\VisorC230`; no incluirlos en el EXE ni dejarlos en el directorio temporal.
- La publicación inicial puede usar un flujo de compilación de una sola ejecución;
  retirar su disparador al terminar. No hay actualizador en línea ni tareas programadas en la app.
