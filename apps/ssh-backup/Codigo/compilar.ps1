$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "Para recompilar instala el SDK de .NET y el Developer Pack de .NET Framework 4.8."
    }
    dotnet build .\SSHBackup.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "La compilación no se ha completado." }
    New-Item -ItemType Directory -Force .\entrega | Out-Null
    Copy-Item .\bin\Release\net48\SSHBackup.exe .\entrega\SSHBackup.exe -Force
    Write-Host "Ejecutable creado: entrega\SSHBackup.exe"
}
finally { Pop-Location }
