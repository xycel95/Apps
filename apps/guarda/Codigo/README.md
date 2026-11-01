# Código de Guarda

- `Program.cs`: ventana WinForms, preferencias y arranque. Conserva compatibilidad con las preferencias de Copia Fácil.
- `BackupEngine.cs`: análisis, copia, comprobación SHA-256, cancelación, informes y finalización de carpetas.
- `app.manifest` y `app.ico`: manifiesto e icono nativos de Windows.
- `Compilar_Windows.bat`: compila con el compilador de .NET Framework instalado en Windows y deja `entrega/Guarda.exe`.
- `build.py` y `winres.py`: compilación en un entorno Linux con Mono extraído. `winres.py` incorpora y comprueba el manifiesto nativo, que mcs 6.8 no incorpora mediante su opción `/win32manifest`.

## Compilar en Windows

Con .NET Framework 4.8 instalado, ejecuta `Compilar_Windows.bat` desde esta carpeta. No necesita paquetes NuGet adicionales. El archivo resultante queda en `../entrega/Guarda.exe`.

## Entorno Linux de desarrollo

`build.py` necesita un entorno Mono extraído con `mono-sgen`, `mcs.exe` y sus referencias. Establece `GUARDA_MONO_ROOT` con su ruta y ejecuta `python3 build.py`. No se incluye ese entorno de desarrollo en las descargas de la aplicación.

Las pruebas están en `../pruebas/`. La prueba de interfaz requiere una pantalla X11 y Mono WinForms. Su ejecución no sustituye la validación nativa en Windows.

Para publicar una nueva versión, coordina versión, binarios, huellas, `app.json`, notas de parche y contexto. La compilación por sí sola no actualiza las descargas publicadas ni el equipo del usuario.
