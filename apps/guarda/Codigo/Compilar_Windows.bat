@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set "GUARDA_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%GUARDA_CSC%" set "GUARDA_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%GUARDA_CSC%" (
  echo No se encuentra el compilador de .NET Framework. Instala .NET Framework 4.8.
  pause
  exit /b 1
)
if not exist "..\entrega" mkdir "..\entrega"
"%GUARDA_CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ /win32manifest:app.manifest /win32icon:app.ico /out:..\entrega\Guarda.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /resource:update-public-key.xml,Guarda.UpdatePublicKey Program.cs BackupEngine.cs Design.cs MainLayout.cs Updates.cs UpdateForm.cs
if errorlevel 1 (
  echo Error al compilar.
  pause
  exit /b 1
)
echo Ejecutable creado en entrega\Guarda.exe
pause
