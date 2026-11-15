$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'Prepare-Runtime.ps1')
    dotnet restore src/Sintonia.App/Sintonia.App.csproj
    if ($LASTEXITCODE -ne 0) { throw 'No se han restaurado las dependencias.' }
    dotnet run --project tests/Sintonia.Checks/Sintonia.Checks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Las comprobaciones han fallado.' }
    dotnet publish src/Sintonia.App/Sintonia.App.csproj -c Release --no-restore -o salida
    if ($LASTEXITCODE -ne 0) { throw 'No se ha podido compilar.' }
    Write-Host 'Listo: salida\Sintonia.exe'
}
finally { Pop-Location }
