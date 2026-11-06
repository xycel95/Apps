param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$code = Split-Path $PSScriptRoot -Parent
$app = Split-Path $code -Parent
$project = Join-Path $code 'BloquesPDF.csproj'
$version = ([xml](Get-Content $project -Raw)).SelectSingleNode('/Project/PropertyGroup/Version').InnerText
& dotnet build $project -c Release -p:RestoreLockedMode=true --nologo
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar Bloques PDF' }
$bin = Join-Path $code 'bin/Release/net48'
$package = Join-Path $OutputDirectory 'package'
if (Test-Path $package) { throw 'Utiliza una carpeta de salida nueva para preparar el paquete.' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Get-ChildItem $bin -File | Where-Object { $_.Extension -in '.exe','.dll','.config' } | Copy-Item -Destination $package
Copy-Item (Join-Path $app 'docs/LEEME.txt') (Join-Path $package 'LEEME.txt')
Copy-Item (Join-Path $app 'docs/LICENCIAS.txt') (Join-Path $package 'LICENCIAS.txt')
$source = Join-Path $package 'CodigoFuente'
New-Item -ItemType Directory -Path $source | Out-Null
foreach ($name in @('BloquesPDF.csproj','packages.lock.json','app.ico','app.manifest','COMPILAR.txt')) {
    Copy-Item (Join-Path $code $name) (Join-Path $source $name)
}
foreach ($folder in @('src','build','tests')) {
    Get-ChildItem (Join-Path $code $folder) -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|work)[\\/]' -and $_.Extension -in '.cs','.csproj','.ps1'
    } | ForEach-Object {
        $relative = $_.FullName.Substring($code.Length).TrimStart('\','/')
        $target = Join-Path $source $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item $_.FullName $target
    }
}
$manifest = @(Get-ChildItem $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path=$_.FullName.Substring($package.Length+1).Replace('\','/'); size_bytes=$_.Length; sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $package 'MANIFIESTO_SHA256.json') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $OutputDirectory ('BloquesPDF_'+$version+'_Windows.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$sums = @()
$sums += ('{0}  {1}' -f ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()), (Split-Path $zip -Leaf))
$sums += ('{0}  BloquesPDF.exe' -f ((Get-FileHash (Join-Path $package 'BloquesPDF.exe') -Algorithm SHA256).Hash.ToLowerInvariant()))
$sums | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('PACKAGE='+$zip)
