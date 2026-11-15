$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$asset = Join-Path $projectRoot 'src/Sintonia.App/Assets/vlc-win-x64.zip'
if (Test-Path $asset) { Write-Host 'Recurso LibVLC ya preparado.'; return }
$cache = Join-Path $projectRoot '.build-cache'
New-Item -ItemType Directory -Force $cache | Out-Null
$package = Join-Path $cache 'videolan.libvlc.windows.3.0.24.nupkg'
$url = 'https://api.nuget.org/v3-flatcontainer/videolan.libvlc.windows/3.0.24/videolan.libvlc.windows.3.0.24.nupkg'
$expected = '3da3f37ee2b2b10e5c788d830a3b513658c36521ea1b0f966ed86eb69f41ef3c'
if (!(Test-Path $package)) { Invoke-WebRequest -Uri $url -OutFile $package }
if ((Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'La huella del paquete LibVLC no coincide con la fijada.' }
Add-Type -AssemblyName System.IO.Compression
$inputArchive = [IO.Compression.ZipFile]::OpenRead($package)
$outputArchive = [IO.Compression.ZipFile]::Open($asset, [IO.Compression.ZipArchiveMode]::Create)
try {
    $prefix = 'build/x64/'
    foreach ($entry in $inputArchive.Entries) {
        if ($entry.FullName.StartsWith($prefix) -and ($entry.FullName.EndsWith('.dll') -or $entry.FullName.EndsWith('.dat'))) {
            $copy = $outputArchive.CreateEntry($entry.FullName.Substring($prefix.Length), [IO.Compression.CompressionLevel]::Optimal)
            $sourceStream = $entry.Open(); $targetStream = $copy.Open()
            try { $sourceStream.CopyTo($targetStream) } finally { $targetStream.Dispose(); $sourceStream.Dispose() }
        }
    }
} finally { $outputArchive.Dispose(); $inputArchive.Dispose() }
Write-Host 'LibVLC 3.0.24 x64 preparado y paquete oficial verificado.'
