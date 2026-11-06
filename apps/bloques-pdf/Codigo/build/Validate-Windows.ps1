param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$code = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$report = [ordered]@{ version='1.0.4'; commit=$env:GITHUB_SHA; checked_at_utc=[DateTime]::UtcNow.ToString('o'); build='pending'; native_tests='pending'; gui='pending'; defender='pending'; packaging='standard-dotnet-exe-with-dlls'; authenticode='not-signed' }
try {
    & (Join-Path $PSScriptRoot 'Pack-Windows.ps1') -OutputDirectory $OutputDirectory
    $report.build='passed'
    $package = Join-Path $OutputDirectory 'package'
    $exe = Join-Path $package 'BloquesPDF.exe'
    $zip = Join-Path $OutputDirectory 'BloquesPDF_1.0.4_Windows.zip'
    $before = @{}
    Get-ChildItem $package -File -Recurse | ForEach-Object { $before[$_.FullName]=(Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    $report.exe_sha256=(Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $report.zip_sha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $report.zip_size_bytes=(Get-Item $zip).Length
    $report.authenticode=(Get-AuthenticodeSignature $exe).Status.ToString()
    $service=Get-Service WinDefend -ErrorAction Stop
    if ($service.Status -ne 'Running') { Start-Service WinDefend; $service.WaitForStatus('Running',[TimeSpan]::FromSeconds(30)) }
    try { Update-MpSignature -ErrorAction Stop } catch { $report.signature_update_note=$_.Exception.Message }
    $status=Get-MpComputerStatus
    if (-not $status.AMServiceEnabled -or -not $status.AntivirusEnabled -or -not $status.AntivirusSignatureVersion) { throw 'Defender no esta disponible: no se considera un analisis valido.' }
    $report.defender_signature=$status.AntivirusSignatureVersion
    $report.defender_engine=$status.AMEngineVersion
    $report.defender_signature_updated=$status.AntivirusSignatureLastUpdated.ToUniversalTime().ToString('o')
    $scanner=@(Get-ChildItem (Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform') -Filter MpCmdRun.exe -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1).FullName
    if (-not $scanner) { $scanner=Join-Path $env:ProgramFiles 'Windows Defender/MpCmdRun.exe' }
    $scanLog=Join-Path $OutputDirectory 'DEFENDER.txt'
    foreach ($target in @($package,$zip)) {
        $oldPreference=$ErrorActionPreference; $ErrorActionPreference='Continue'
        $scan=& $scanner -Scan -ScanType 3 -File $target -DisableRemediation 2>&1
        $scanExit=$LASTEXITCODE; $ErrorActionPreference=$oldPreference
        $scan | Out-File $scanLog -Append -Encoding utf8
        $scan | ForEach-Object { Write-Output $_ }
        if ($scanExit -ne 0) { throw ('Defender no termino limpio. Codigo '+$scanExit) }
    }
    foreach ($file in $before.Keys) {
        if (-not (Test-Path $file) -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $before[$file]) { throw 'Un componente ha cambiado o se ha retirado durante el analisis.' }
    }
    if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $report.zip_sha256) { throw 'El ZIP ha cambiado durante el analisis.' }
    $report.defender='no-threats-reported'
    & dotnet build (Join-Path $code 'tests/windows/NativeTests.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas nativas' }
    $native=Join-Path $code 'tests/windows/bin/Release/net48/NativeTests.exe'
    $nativeOutput=& $native (Join-Path $OutputDirectory 'test-data') 2>&1
    $nativeExit=$LASTEXITCODE
    $nativeOutput | ForEach-Object { Write-Output $_ }
    if ($nativeExit -ne 0) { throw 'Las pruebas nativas no han terminado correctamente' }
    $checks=[regex]::Match(($nativeOutput | Out-String),'NATIVE_CHECKS_PASSED=(\d+)')
    if (-not $checks.Success) { throw 'Las pruebas no informan de sus comprobaciones' }
    $report.native_tests='passed'; $report.native_checks=[int]$checks.Groups[1].Value
    $process=Start-Process $exe -WorkingDirectory $package -PassThru
    try {
        Start-Sleep -Seconds 3; $process.Refresh()
        if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw 'El EXE distribuido no ha abierto su ventana' }
        $report.window_title=$process.MainWindowTitle
        if (-not $process.CloseMainWindow()) { throw 'No se pudo cerrar normalmente la ventana' }
        if (-not $process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'El EXE no ha cerrado correctamente' }
        $report.gui='passed'
    } finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
    $report.status='passed'
} catch { $report.status='failed'; $report.error=$_.Exception.ToString(); throw }
finally {
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'VALIDACION-WINDOWS.json') -Encoding UTF8
    Write-Output ($report | ConvertTo-Json -Depth 8 -Compress)
}
