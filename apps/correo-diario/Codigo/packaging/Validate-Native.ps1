$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
$sdk = (& dotnet --version | Out-String).Trim()
Pop-Location
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class CorreoDiarioWindowCheck {
  public delegate bool EnumProc(IntPtr window, IntPtr parameter);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr parameter);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder text, int maximum);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
  public static IntPtr FindText(IntPtr parent, string caption) {
    IntPtr found=IntPtr.Zero;
    EnumChildWindows(parent, (window, parameter) => {
      var text=new StringBuilder(1024); GetWindowText(window,text,1024);
      if(text.ToString()==caption) {found=window; return false;}
      return true;
    },IntPtr.Zero);
    return found;
  }
  public static int CountClass(IntPtr parent, string name) {
    int count=0;
    EnumChildWindows(parent, (window, parameter) => {
      var text=new StringBuilder(256); GetClassName(window,text,256);
      var className=text.ToString();
      if(className==name || className.StartsWith("WindowsForms10."+name+".",StringComparison.Ordinal)) count++;
      return true;
    },IntPtr.Zero);
    return count;
  }
}
'@

$exe = Join-Path $env:GITHUB_WORKSPACE '.correo-publish/CorreoDiario.exe'
$process = Start-Process -FilePath $exe -PassThru
try {
  for ($i=0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) { throw 'El ejecutable terminó antes de mostrar la ventana.' }
    if ($process.MainWindowHandle -ne [IntPtr]::Zero -and $process.MainWindowTitle -like 'CorreoDiario*') { break }
  }
  if ($process.MainWindowHandle -eq [IntPtr]::Zero -or $process.MainWindowTitle -notlike 'CorreoDiario*') { throw 'No apareció la ventana de CorreoDiario.' }
  $checkbox = [CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Eliminar del buzón después de verificar la exportación')
  if ($checkbox -eq [IntPtr]::Zero) { throw 'No se encuentra la casilla de borrado en la ventana.' }
  $state = [CorreoDiarioWindowCheck]::SendMessage($checkbox, 0x00F0, [IntPtr]::Zero, [IntPtr]::Zero).ToInt64()
  if ($state -ne 0) { throw 'La casilla de borrado no está desmarcada al arrancar.' }
  if ([CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Buscar pestañas') -eq [IntPtr]::Zero) { throw 'Falta el selector de navegador.' }
  if ([CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Usar esta pestaña') -eq [IntPtr]::Zero) { throw 'Falta la conexión a la pestaña seleccionada.' }
  if ([CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Cómo conectar') -eq [IntPtr]::Zero) { throw 'Falta la guía de conexión.' }
  if ([CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Abrir Outlook') -ne [IntPtr]::Zero) { throw 'Sigue existiendo el inicio de sesión integrado.' }
  if ([CorreoDiarioWindowCheck]::CountClass($process.MainWindowHandle, 'SysDateTimePick32') -ne 2 -or
      [CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Desde') -eq [IntPtr]::Zero -or
      [CorreoDiarioWindowCheck]::FindText($process.MainWindowHandle, 'Hasta') -eq [IntPtr]::Zero) { throw 'Faltan los dos controles del intervalo de fechas.' }
  $report = [ordered]@{
    native_startup_verified = $true
    deletion_checkbox_found = $true
    deletion_checkbox_default_checked = $false
    existing_browser_ui_verified = $true
    embedded_login_ui_absent = $true
    date_range_controls_verified = $true
    window_title = $process.MainWindowTitle
    windows = [Environment]::OSVersion.VersionString
    dotnet_sdk = $sdk
    source_commit = $env:GITHUB_SHA
    workflow_url = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    pdf_render_verified = $false
    real_browser_attachment_verified = $false
    outlook_profile_verified = $false
    user_mail_accessed = $false
    limitation = 'Arranque e interfaz comprobados; el informe de empaquetado añade la prueba autenticada con Edge y servicio HTTPS sintético. Pendientes las cuentas y la impresión del usuario.'
  }
  $report | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 .correo-release/VALIDACION-WINDOWS.json
  Write-Host 'PASS Arranque Windows: selector de pestañas, sin inicio de sesión integrado y borrado desmarcado.'
} finally {
  if (-not $process.HasExited) {
    $null = $process.CloseMainWindow()
    if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force }
  }
}
