param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$code = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path $code) 'entrega' }
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$compiler = $null
if (Test-Path $vswhere) { $compiler = @(& $vswhere -latest -products '*' -find 'MSBuild\**\Bin\Roslyn\csc.exe') | Select-Object -First 1 }
if (-not $compiler) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe' }
if (-not (Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $compiler)) { throw 'No se encuentra un compilador de Microsoft C#.' }
$common = @('/nologo','/noconfig','/nostdlib+','/codepage:65001','/target:winexe','/platform:anycpu','/optimize+','/debug-',('/win32manifest:' + (Join-Path $code 'app.manifest')),('/win32icon:' + (Join-Path $code 'app.ico')),('/resource:' + (Join-Path $code 'update-public-key.xml') + ',Guarda.UpdatePublicKey'),('/out:' + (Join-Path $OutputDirectory 'Guarda.exe')))
if ($compiler -like '*Roslyn*') { $common += @('/deterministic+',('/pathmap:' + $code + '=/_/Guarda')) }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $framework)) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$sources = @(Get-ChildItem $code -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
& $compiler @common @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar Guarda.' }
$output = Join-Path $OutputDirectory 'Guarda.exe'
Write-Output ('Compilador: ' + $compiler)
Write-Output ('Ejecutable: ' + $output)
Get-FileHash $output -Algorithm SHA256 | Format-List
