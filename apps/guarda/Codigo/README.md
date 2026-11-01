# Código de Guarda

- `Program.cs`: ventana WinForms, preferencias y arranque. Conserva compatibilidad con las preferencias de Copia Fácil.
- `MainLayout.cs` y `Design.cs`: nueva composición, controles y temas oscuro/claro.
- `Updates.cs` y `UpdateForm.cs`: consulta del canal firmado, descarga, verificación e instalación con reinicio y restauración ante fallo.
- `update-public-key.xml`: clave pública integrada en los clientes. No rotarla en una actualización normal.
- `firmar_actualizacion.py`: firma con la clave privada conservada fuera del repositorio. Ver [Publicar una actualización](../docs/PUBLICAR-ACTUALIZACION.md).
- `BackupEngine.cs`: análisis, copia, comprobación SHA-256, cancelación, informes y finalización de carpetas.
- `app.manifest` y `app.ico`: manifiesto e icono nativos de Windows.
- `Compilar_Windows.bat`: compila con el compilador de .NET Framework instalado en Windows y deja `entrega/Guarda.exe`.
- `Compilar_Windows.ps1`: compilación de distribución con Microsoft Roslyn cuando está instalado Visual Studio; incorpora icono, recursos y manifiesto en una pasada. Puede usar el compilador de .NET Framework como alternativa local.
- `Validar_Windows.ps1`: compilación con Roslyn, análisis de Defender, pruebas de copias y actualizador, temas y actualización nativa entre procesos. Recoge por separado la salida y los errores de cada proceso y comprueba su código de salida.
- `build.py` y `winres.py`: compilación de desarrollo con Roslyn del SDK .NET en Linux y verificación del manifiesto nativo. Mono se utiliza para ejecutar las pruebas.

## Compilar en Windows

Con .NET Framework 4.8 instalado, ejecuta `Compilar_Windows.ps1` desde PowerShell. Para reproducir la compilación de distribución se necesita Roslyn de Visual Studio. `Compilar_Windows.bat` ofrece la alternativa con el compilador de .NET Framework. No se necesitan paquetes NuGet adicionales. El archivo resultante queda en `../entrega/Guarda.exe`.

## Entorno Linux de desarrollo

`build.py` necesita el SDK .NET con Roslyn y las referencias de .NET Framework proporcionadas por Mono. Establece `GUARDA_DOTNET_ROOT` y `GUARDA_MONO_ROOT` con sus rutas y ejecuta `python3 build.py`. También admite `GUARDA_CSC_DLL` y `GUARDA_FRAMEWORK_REFS`. El entorno de desarrollo no se incluye en las descargas.

Las pruebas están en `../pruebas/`. La prueba de interfaz requiere una pantalla X11 y Mono WinForms. Su ejecución no sustituye la validación nativa en Windows.

`UpdateTests.exe` recibe el canal firmado y su ejecutable: `UpdateTests.exe RUTA/stable.xml RUTA/Guarda.exe`. Comprueba firma, archivo y sustitución con restauración. Las pruebas no incluyen la clave privada.

Para publicar una nueva versión, coordina versión, binarios, huellas, `app.json`, notas de parche y contexto. La compilación por sí sola no actualiza las descargas publicadas ni el equipo del usuario.
