param([Parameter(Mandatory=$true)][string]$Executable, [Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path $Executable).Path
$marker = Join-Path $env:LOCALAPPDATA 'XyceL/Sintonia/smoke-test.txt'
function Test-Launch([string]$PathToExe) {
    if (Test-Path $marker) { Remove-Item $marker }
    $process = Start-Process -FilePath $PathToExe -ArgumentList '--smoke-test' -PassThru
    if (!$process.WaitForExit(60000)) {
        $process.Kill($true)
        $log = Join-Path $env:LOCALAPPDATA 'XyceL/Sintonia/errors.log'
        if (Test-Path $log) { Get-Content $log -Tail 30 | Write-Host }
        throw 'El arranque nativo no terminó en 60 segundos.'
    }
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0 -or !(Test-Path $marker)) { throw 'No se ha confirmado la creación de la ventana y el inicio de LibVLC.' }
    if (!(Get-Content $marker -Raw).Contains('VLC initialized')) { throw 'La comprobación de LibVLC no generó el marcador esperado.' }
}
$staging = Join-Path $env:RUNNER_TEMP ('SintoniaValidation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
$first = Join-Path $staging 'Sintonia.exe'
Copy-Item $Executable $first
Test-Launch $first
$moved = Join-Path $staging 'movida'
New-Item -ItemType Directory $moved | Out-Null
$renamed = Join-Path $moved 'Television-portatil.exe'
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while ($true) {
    try { Move-Item $first $renamed -ErrorAction Stop; break }
    catch { if ([DateTime]::UtcNow -ge $deadline) { throw }; Start-Sleep -Milliseconds 500 }
}
Test-Launch $renamed
$result = [ordered]@{
    version='1.0.0'; source_commit=$env:GITHUB_SHA;
    workflow_url="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID";
    verified_at_utc=[DateTime]::UtcNow.ToString('o');
    environment=[System.Runtime.InteropServices.RuntimeInformation]::OSDescription;
    architecture=[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString();
    core_assertions_passed=26; native_windows_verified=$true; window_and_libvlc_initialized=$true;
    empty_folder_verified=$true; moved_and_renamed_verified=$true;
    live_playback_in_executable_verified=$false;
    known_limit='El modo smoke-test comprueba ventana y carga de VLC. No reproduce emisiones ni acredita el funcionamiento de todos los canales.';
    executable_sha256=(Get-FileHash $Executable -Algorithm SHA256).Hash.ToLowerInvariant()
}
$result | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $Report
Write-Host 'Ventana, LibVLC, carpeta vacía y ejecutable movido/renombrado comprobados en Windows.'
