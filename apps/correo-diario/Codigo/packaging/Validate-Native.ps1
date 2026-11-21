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
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
  public static IntPtr Checkbox(IntPtr parent) {
    IntPtr found=IntPtr.Zero;
    EnumChildWindows(parent, (window, parameter) => {
      var text=new StringBuilder(1024); GetWindowText(window,text,1024);
      if(text.ToString()=="Eliminar del buzón después de verificar la exportación") {found=window; return false;}
      return true;
    },IntPtr.Zero);
    return found;
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
  $checkbox = [CorreoDiarioWindowCheck]::Checkbox($process.MainWindowHandle)
  if ($checkbox -eq [IntPtr]::Zero) { throw 'No se encuentra la casilla de borrado en la ventana.' }
  $state = [CorreoDiarioWindowCheck]::SendMessage($checkbox, 0x00F0, [IntPtr]::Zero, [IntPtr]::Zero).ToInt64()
  if ($state -ne 0) { throw 'La casilla de borrado no está desmarcada al arrancar.' }
  $report = [ordered]@{
    native_startup_verified = $true
    deletion_checkbox_found = $true
    deletion_checkbox_default_checked = $false
    window_title = $process.MainWindowTitle
    windows = [Environment]::OSVersion.VersionString
    dotnet_sdk = $sdk
    source_commit = $env:GITHUB_SHA
    workflow_url = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    pdf_render_verified = $false
    web_login_verified = $false
    outlook_profile_verified = $false
    user_mail_accessed = $false
    limitation = 'Prueba de arranque y casilla por defecto; no autentica cuentas, no imprime correo ni realiza movimientos reales.'
  }
  $report | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 .correo-release/VALIDACION-WINDOWS.json
  Write-Host 'PASS Arranque Windows: ventana de CorreoDiario y casilla desmarcada.'
} finally {
  if (-not $process.HasExited) {
    $null = $process.CloseMainWindow()
    if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force }
  }
}
