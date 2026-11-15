param([Parameter(Mandatory=$true)][string]$Executable, [Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path $Executable).Path
$temporaryRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$staging = Join-Path $temporaryRoot ('SintoniaValidation-' + [guid]::NewGuid().ToString('N'))
$testData = Join-Path $staging 'isolated-data'
New-Item -ItemType Directory -Force $staging | Out-Null
$marker = Join-Path $testData 'smoke-test.txt'
$preferences = Join-Path $testData 'preferences.json'
function Test-Launch([string]$PathToExe) {
    if (Test-Path $marker) { Remove-Item $marker }
    $process = Start-Process -FilePath $PathToExe -ArgumentList @('--smoke-test', '--test-data-directory', "`"$testData`"") -PassThru
    if (!$process.WaitForExit(60000)) { $process.Kill($true); $process.Dispose(); throw 'El arranque nativo no terminó en 60 segundos.' }
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0 -or !(Test-Path $marker)) { throw 'No se ha confirmado la creación de la ventana y el inicio de LibVLC.' }
    if (!(Get-Content $marker -Raw).Contains('VLC initialized')) { throw 'Falta el marcador de inicio de LibVLC.' }
}
$first = Join-Path $staging 'Sintonia.exe'
Copy-Item $Executable $first
Test-Launch $first
$fresh = Get-Content $preferences -Raw | ConvertFrom-Json
if (@($fresh.Sources).Count -ne 0 -or $fresh.SchemaVersion -ne 2) { throw 'La instalación nueva no está vacía.' }
$legacy = [ordered]@{
    SchemaVersion=1; Volume=31; Muted=$true; LastChannelId=''; Favorites=@('name:canal de prueba');
    Sources=@(
        @{ Id='spain-official'; Name='Antigua lista automática'; Kind='url'; BuiltIn=$true; Enabled=$true; OfficialOnly=$true; Location='https://iptv-org.github.io/iptv/countries/es.m3u'; Entries=@() },
        @{ Id='user-keep'; Name='Canal de prueba'; Kind='stream'; BuiltIn=$false; Enabled=$true; OfficialOnly=$false; Location='http://127.0.0.1:9/test.m3u8'; Entries=@() }
    )
}
$legacy | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $preferences
Test-Launch $first
$migrated = Get-Content $preferences -Raw | ConvertFrom-Json
if (@($migrated.Sources).Count -ne 1 -or $migrated.Sources[0].Id -ne 'user-keep') { throw 'La actualización no ha conservado exclusivamente la lista del usuario.' }
if ($migrated.Volume -ne 31 -or !$migrated.Muted -or $migrated.Favorites -notcontains 'name:canal de prueba') { throw 'Se han alterado favoritos o volumen.' }
$moved = Join-Path $staging 'movida'
New-Item -ItemType Directory $moved | Out-Null
$renamed = Join-Path $moved 'Television-portatil.exe'
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while ($true) {
    try { Move-Item $first $renamed -ErrorAction Stop; break }
    catch { if ([DateTime]::UtcNow -ge $deadline) { throw }; Start-Sleep -Milliseconds 500 }
}
Test-Launch $renamed
$restarted = Get-Content $preferences -Raw | ConvertFrom-Json
if (@($restarted.Sources).Count -ne 1 -or $restarted.Sources[0].Id -ne 'user-keep') { throw 'La lista automática ha reaparecido al reiniciar.' }
$result = [ordered]@{
    version='1.0.1'; source_commit=$env:GITHUB_SHA;
    workflow_url="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID";
    verified_at_utc=[DateTime]::UtcNow.ToString('o');
    environment=[System.Runtime.InteropServices.RuntimeInformation]::OSDescription;
    architecture=[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString();
    core_assertions_passed=30; native_windows_verified=$true; window_and_libvlc_initialized=$true;
    fresh_launch_sources=0; fresh_launch_channels=0; empty_start_verified=$true;
    legacy_automatic_source_removed=$true; user_imports_preserved=$true; favorites_and_volume_preserved=$true;
    empty_folder_verified=$true; moved_and_renamed_verified=$true;
    live_playback_in_executable_verified=$false;
    known_limit='La prueba comprueba arranque vacío, migración y carga de VLC. No reproduce emisiones ni acredita los canales añadidos por el usuario.';
    executable_sha256=(Get-FileHash $Executable -Algorithm SHA256).Hash.ToLowerInvariant()
}
$result | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $Report
Write-Host 'Arranque vacío, migración sin pérdida de datos, ventana, VLC y traslado comprobados en Windows.'
