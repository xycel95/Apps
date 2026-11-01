$ErrorActionPreference = 'Stop'
$app = Split-Path $PSScriptRoot -Parent
$code = Join-Path $app 'Codigo'
$stage = Join-Path $env:RUNNER_TEMP ('SSH Backup prueba ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage | Out-Null
$env:SSHBACKUP_DATA_DIR = Join-Path $stage 'configuracion'
$env:SSHBACKUP_TEST_PACKAGE = Join-Path $app 'descargas/versiones/2.2.1/SSHBackup_2.2.1.sshupdate'
$sdk = (& dotnet --version).Trim()
$dotnet = (Get-Command dotnet).Source
$csc = Join-Path (Split-Path $dotnet -Parent) "sdk/$sdk/Roslyn/bincore/csc.dll"
$refs = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
if (!(Test-Path $csc) -or !(Test-Path $refs)) { throw 'Faltan Roslyn o las referencias de .NET Framework 4.8.' }
$common = @('/nologo','/noconfig','/nostdlib+','/langversion:7.3','/platform:anycpu','/optimize+')
foreach ($name in @('mscorlib','System','System.Core','System.Xml','System.Security','System.IO.Compression','System.Windows.Forms','System.Drawing')) { $common += "/r:$refs/$name.dll" }
function Compile([string]$output, [string[]]$sources, [string[]]$extra) {
    & $dotnet $csc @common "/out:$output" @extra @sources
    if ($LASTEXITCODE -ne 0) { throw 'La compilación ha fallado.' }
}
function CheckExit([string]$label) { if ($LASTEXITCODE -ne 0) { throw "Error en $label" } }
try {
    # Compilación independiente de la fuente publicada, sin regenerar ni firmar la entrega.
    & dotnet build (Join-Path $code 'SSHBackup.csproj') --configuration Release
    CheckExit 'compilar la aplicación'
    $export = Join-Path $PSScriptRoot 'framework-refs'
    New-Item -ItemType Directory -Force $export | Out-Null
    foreach ($name in @('mscorlib','System','System.Core','System.Xml','System.Security','System.IO.Compression','System.Windows.Forms','System.Drawing')) { Copy-Item "$refs/$name.dll" $export }
    New-Item -ItemType Directory -Force (Join-Path $export 'RedistList') | Out-Null
    Copy-Item (Join-Path $refs 'RedistList/FrameworkList.xml') (Join-Path $export 'RedistList/FrameworkList.xml')
    $compiled = Join-Path $PSScriptRoot 'compilado'
    New-Item -ItemType Directory -Force $compiled | Out-Null
    Copy-Item (Join-Path $code 'bin/Release/net48/SSHBackup.exe') (Join-Path $compiled 'SSHBackup.exe')
    $exe = Join-Path $stage 'SSHBackup.exe'
    Copy-Item (Join-Path $app 'descargas/SSHBackup.exe') $exe
    if ([Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString() -ne '2.2.1.0') { throw 'Versión de entrega incorrecta.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $app 'descargas/SSHBackup_Completo_2.1.0.zip'))
    try {
        $entry = $archive.GetEntry('SSHBackup.exe')
        if (!$entry) { throw 'No se encuentra el ejecutable anterior.' }
        $input = $entry.Open(); $output = [IO.File]::Create((Join-Path $stage 'old.exe'))
        try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
    } finally { $archive.Dispose() }
    $legacyQuote = $false
    try { $legacy = [Reflection.Assembly]::LoadFile((Join-Path $stage 'old.exe')); $quoted = $legacy.GetType('SSHBackup.Updates').GetMethod('Quote').Invoke($null, @('C:\Ruta de prueba\SSHBackup.exe')); $legacyQuote = $quoted.StartsWith('"') } catch { Write-Host 'El cliente anterior necesita sustituir el EXE manualmente para el salto inicial.' }
    $tests = Join-Path $stage 'TestsUpdates.exe'
    Compile $tests @((Join-Path $code 'TestsUpdates.cs')) @('/target:exe','/main:SSHBackup.TestsUpdates',"/r:$exe")
    & $tests unit (Join-Path $stage 'unit') $env:SSHBACKUP_TEST_PACKAGE (Join-Path $stage 'old.exe')
    CheckExit 'firma, integridad, migración, sustitución y restauración'
    & $tests gui (Join-Path $stage 'gui')
    CheckExit 'interfaz de actualizaciones'
    if ($env:GITHUB_REF_NAME -eq 'ssh-backup') {
        & $tests online (Join-Path $stage 'online')
        CheckExit 'consulta y descarga HTTPS pública desde la aplicación'
    }
    # El host con versión 2.1 ensaya el saludo listo/confirmación/salida/reemplazo/reinicio.
    $probe = Join-Path $stage 'SSH Backup ensayo.exe'
    Compile $probe @((Join-Path $PSScriptRoot 'NativeUpdateProbe.cs'),(Join-Path $code 'Updates.cs')) @('/target:exe','/main:SSHBackup.Program')
    $before = (Get-FileHash $probe -Algorithm SHA256).Hash
    $expected = (Get-FileHash $exe -Algorithm SHA256).Hash
    $parent = Start-Process -FilePath $probe -ArgumentList @('--probe',('"' + $env:SSHBACKUP_TEST_PACKAGE + '"')) -PassThru
    if (!$parent.WaitForExit(45000) -or $parent.ExitCode -ne 0) { throw 'El instalador no confirmó que estaba preparado.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $restarted = $null
    do {
        Start-Sleep -Milliseconds 400
        $restarted = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $probe -and $_.Id -ne $parent.Id -and $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -like 'SSH Backup 2.2.1*' } | Select-Object -First 1
    } while (!$restarted -and [DateTime]::UtcNow -lt $deadline)
    if (!$restarted -or (Get-FileHash $probe -Algorithm SHA256).Hash -ne $expected) { throw 'No se ha confirmado el reinicio de la entrega real.' }
    if ((Get-FileHash ($probe + '.previous') -Algorithm SHA256).Hash -ne $before) { throw 'No se conserva el ejecutable anterior.' }
    $report = [ordered]@{ version='2.2.1'; environment='Windows GitHub Actions'; compiled=$true; signed_update_tests=16; gui_tests=6; ready_handshake=$true; replaced_and_restarted=$true; previous_preserved=$true; production_servers_tested=$false; task_scheduler_executed=$false; legacy_package_quote_supported=$legacyQuote; online_publication_tested=($env:GITHUB_REF_NAME -eq 'ssh-backup') }
    $report | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'VALIDACION-WINDOWS.json') -Encoding utf8
    Write-Host ($report | ConvertTo-Json)
} finally {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($stage) } | Stop-Process -Force -ErrorAction SilentlyContinue
}
