# Código de Sintonía 1.0.1

Instala el SDK oficial .NET 10 y ejecuta en PowerShell desde esta carpeta:

```powershell
./packaging/build.ps1
```

El script obtiene LibVLC 3.0.24 del paquete oficial de NuGet, verifica su SHA-256,
prepara el recurso del reproductor, ejecuta las 26 comprobaciones y compila
`salida/Sintonia.exe`. No requiere Python ni tener VLC instalado.

La descarga completa de código incluye el recurso nativo ya preparado y los
archivos originales de fuentes de VLC y LibVLCSharp. No se guardan esos archivos
grandes en el historial Git. Consulta packaging/UPSTREAM-SOURCES.txt.

- src/Sintonia.Core: catálogo, parser M3U, filtro, HTTP y preferencias JSON.
- src/Sintonia.App: interfaz, reproducción y extracción del motor.
- tests/Sintonia.Checks: comprobaciones de comportamiento con HTTP local.

El ejecutable puede moverse sin archivos adyacentes. El motor se prepara en
`%LOCALAPPDATA%\XyceL\Sintonia\runtime`; los ajustes quedan en el mismo directorio
de usuario. Nunca se incluyen ajustes reales en una distribución.

La validación vigente se registra en [Descargar](../descargas/README.md) y en
docs/VALIDACION-WINDOWS.json. El modo `--smoke-test` crea la ventana, inicializa
LibVLC, escribe smoke-test.txt en el directorio de datos y cierra la aplicación.
Esa comprobación de arranque no demuestra reproducción de todos los directos.

La aplicación no incorpora listas ni catálogos. La migración del esquema 1 al 2
retira únicamente las fuentes marcadas BuiltIn o con el ID histórico spain-official.
Las pruebas nativas usan un directorio de datos temporal aislado.
