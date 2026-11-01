param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$code = $PSScriptRoot
$app = Split-Path $code
$tests = Join-Path $app 'pruebas'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $env:TEMP ('Guarda-validacion-' + [guid]::NewGuid().ToString('N')) }
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
$report = [ordered]@{ version='1.1.1'; source_commit=$env:GITHUB_SHA; environment=[Environment]::OSVersion.VersionString; build='pending'; defender='pending'; core='pending'; updater='pending'; gui='pending'; native_update_cycle='pending' }

function Run-Checked([string]$Exe,[string[]]$Arguments,[string]$Log) {
  # Windows PowerShell 5.1 can turn redirected native stderr into a terminating
  # NativeCommandError before the executable's exit code or full error is read.
  $process = New-Object System.Diagnostics.Process
  $process.StartInfo.FileName = $Exe
  $process.StartInfo.Arguments = (@($Arguments | ForEach-Object { '"' + ($_ -replace '(\\+)$','$1$1') + '"' }) -join ' ')
  $process.StartInfo.UseShellExecute = $false
  $process.StartInfo.CreateNoWindow = $true
  $process.StartInfo.RedirectStandardOutput = $true
  $process.StartInfo.RedirectStandardError = $true
  $process.StartInfo.StandardOutputEncoding = [Text.Encoding]::UTF8
  $process.StartInfo.StandardErrorEncoding = [Text.Encoding]::UTF8
  try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) {
      $process.Kill()
      throw ($Exe + ' superó el tiempo máximo de 60 segundos; revisar ' + $Log)
    }
    $process.WaitForExit()
    $outputText=$stdout.Result; $errorText=$stderr.Result; $exitCode=$process.ExitCode
    [IO.File]::WriteAllText($Log,$outputText,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText(($Log+'.stderr'),$errorText,[Text.UTF8Encoding]::new($false))
    if ($outputText) { Write-Output $outputText.TrimEnd() }
    if ($errorText) { Write-Output $errorText.TrimEnd() }
    if ($exitCode -ne 0) { throw ($Exe + ' terminó con código ' + $exitCode + '; revisar ' + $Log + ' y ' + $Log + '.stderr') }
  } finally {
    $process.Dispose()
  }
}

try {
  & (Join-Path $code 'Compilar_Windows.ps1') -OutputDirectory $OutputDirectory
  $exe = Join-Path $OutputDirectory 'Guarda.exe'
  $report.build='passed'
  $report.sha256=(Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
  $report.size_bytes=(Get-Item $exe).Length
  $report.authenticode=(Get-AuthenticodeSignature $exe).Status.ToString()
  $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
  $csc = @(& $vswhere -latest -products '*' -find 'MSBuild\**\Bin\Roslyn\csc.exe') | Select-Object -First 1
  if (-not $csc) { throw 'Se necesita Roslyn de Microsoft para validar esta entrega.' }
  $report.compiler=(& $csc /version | Out-String).Trim()
  $framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
  $refs=@('mscorlib','System','System.Core','System.Xml','System.Drawing','System.Windows.Forms') | ForEach-Object { '/r:'+(Join-Path $framework ($_+'.dll')) }
  $common=@('/nologo','/noconfig','/nostdlib+','/codepage:65001','/langversion:5','/platform:anycpu','/optimize+','/deterministic+','/debug-')
  $sources=@(Get-ChildItem $code -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })

  $coreExe=Join-Path $OutputDirectory 'Tests.exe'
  & $csc @common @refs /target:exe /main:Guarda.Tests ('/out:'+$coreExe) (Join-Path $code 'BackupEngine.cs') (Join-Path $tests 'Tests.cs')
  if ($LASTEXITCODE -ne 0) { throw 'No se compilan las pruebas del motor.' }
  $coreLog=Join-Path $OutputDirectory 'core.txt'
  Run-Checked $coreExe @() $coreLog
  if ([IO.File]::ReadAllText($coreLog) -notmatch 'RESULTADO:\s*(\d+)\s*pruebas') { throw 'Falta el recuento de pruebas del motor.' }
  $report.core=($Matches[1]+'-passed')

  # The test key is ephemeral and different from the production update key.
  $rsa=New-Object System.Security.Cryptography.RSACryptoServiceProvider -ArgumentList 3072
  $rsa.PersistKeyInCsp=$false
  $testPublic=Join-Path $OutputDirectory 'test-public.xml'
  [IO.File]::WriteAllText($testPublic,$rsa.ToXmlString($false),[Text.UTF8Encoding]::new($false))
  $xml=New-Object System.Xml.XmlDocument
  $root=$xml.CreateElement('GuardaUpdate');$root.SetAttribute('schema','1');$root.SetAttribute('app','guarda');[void]$xml.AppendChild($root)
  $fields=[ordered]@{ Version='1.1.1';Url='https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/versiones/1.1.1/Guarda.exe';Sha256=$report.sha256;Size=[string]$report.size_bytes;Notes='Fixture de validación nativa; no es el canal de producción.' }
  foreach($key in $fields.Keys) { $node=$xml.CreateElement($key);$node.InnerText=$fields[$key];[void]$root.AppendChild($node) }
  $manifest=Join-Path $OutputDirectory 'test-stable.xml'
  $bytes=[Text.Encoding]::UTF8.GetBytes($xml.OuterXml)
  [IO.File]::WriteAllBytes($manifest,$bytes)
  [IO.File]::WriteAllText(($manifest+'.sig'),[Convert]::ToBase64String($rsa.SignData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))))
  $testResource='/resource:'+$testPublic+',Guarda.UpdatePublicKey'
  $updateExe=Join-Path $OutputDirectory 'UpdateTests.exe'
  & $csc @common @refs $testResource /target:exe /main:Guarda.UpdateTests ('/out:'+$updateExe) (Join-Path $code 'Updates.cs') (Join-Path $tests 'UpdateTests.cs')
  if ($LASTEXITCODE -ne 0) { throw 'No se compilan las pruebas del actualizador.' }
  Run-Checked $updateExe @($manifest,$exe) (Join-Path $OutputDirectory 'updater.txt'); $report.updater='13-passed'

  $guiExe=Join-Path $OutputDirectory 'GuiSmoke.exe'
  & $csc @common @refs ('/resource:'+(Join-Path $code 'update-public-key.xml')+',Guarda.UpdatePublicKey') /target:exe /main:Guarda.GuiSmoke ('/out:'+$guiExe) @sources (Join-Path $tests 'GuiSmoke.cs')
  if ($LASTEXITCODE -ne 0) { throw 'No se compila la prueba de la ventana.' }
  Run-Checked $guiExe @((Join-Path $OutputDirectory 'imagenes')) (Join-Path $OutputDirectory 'gui.txt'); $report.gui='copy-and-themes-passed'

  $oldFolder=Join-Path $OutputDirectory 'old';New-Item $oldFolder -ItemType Directory -Force | Out-Null
  $oldProgram=Join-Path $oldFolder 'Program.cs'
  [IO.File]::WriteAllText($oldProgram,[IO.File]::ReadAllText((Join-Path $code 'Program.cs')).Replace('1.1.1.0','1.1.0.0'),[Text.UTF8Encoding]::new($false))
  $otherSources=@($sources | Where-Object { [IO.Path]::GetFileName($_) -ne 'Program.cs' })
  $oldExe=Join-Path $oldFolder 'Guarda.exe'
  & $csc @common @refs $testResource /target:winexe /main:Guarda.NativeUpdateProbe ('/out:'+$oldExe) $oldProgram @otherSources (Join-Path $tests 'NativeUpdateProbe.cs')
  if ($LASTEXITCODE -ne 0) { throw 'No se compila el arranque de prueba de la versión anterior.' }
  Run-Checked $oldExe @($manifest,$exe) (Join-Path $OutputDirectory 'native-update.txt')
  $started=$null;$watch=[Diagnostics.Stopwatch]::StartNew()
  while($watch.ElapsedMilliseconds -lt 40000) {
    $started=@(Get-Process -Name Guarda -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $oldExe -and $_.MainWindowHandle -ne 0 }) | Select-Object -First 1
    if($started -and (Get-FileHash $oldExe -Algorithm SHA256).Hash.ToLowerInvariant() -eq $report.sha256) { break }
    Start-Sleep -Milliseconds 200
  }
  if(-not $started -or (Get-FileHash $oldExe -Algorithm SHA256).Hash.ToLowerInvariant() -ne $report.sha256) { throw 'La nueva versión no ha sustituido el programa y abierto su ventana.' }
  if(-not @(Get-ChildItem $oldFolder -Filter '*.bak').Count) { throw 'No se conserva la copia anterior.' }
  [void]$started.CloseMainWindow();if(-not $started.WaitForExit(5000)) { Stop-Process -Id $started.Id -Force }
  $report.native_update_cycle='parent-exit-replace-restart-passed'
  $rsa.Dispose()

  $defenderService=Get-Service -Name WinDefend -ErrorAction Stop
  if ($defenderService.Status -ne 'Running') {
    Start-Service -Name WinDefend -ErrorAction Stop
    $defenderService.WaitForStatus('Running',[TimeSpan]::FromSeconds(20))
  }
  $defenderService.Refresh()
  if ($defenderService.Status -ne 'Running') { throw 'El servicio de Microsoft Defender no está activo.' }
  $report.defender_service=$defenderService.Status.ToString()
  $scanner=@(Get-ChildItem (Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform') -Filter MpCmdRun.exe -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1).FullName
  if (-not $scanner) { $scanner=Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe' }
  $report.defender_scanner_version=[Diagnostics.FileVersionInfo]::GetVersionInfo($scanner).ProductVersion
  try { Run-Checked $scanner @('-SignatureUpdate') (Join-Path $OutputDirectory 'defender-update.txt') } catch { $report.signature_update_note=$_.Exception.Message }
  # The CIM management provider can fail independently of the scanning engine.
  # Record that limitation, but require the running service AND a successful,
  # explicit scan with an affirmative no-threats result from Microsoft's CLI.
  $status=$null
  try { $status=Get-MpComputerStatus -ErrorAction Stop } catch { $report.defender_status_note=$_.Exception.Message }
  if ($status) {
    if (-not $status.AMServiceEnabled -or -not $status.AntivirusEnabled) { throw 'Microsoft Defender informa de que el antivirus no está activo.' }
    $report.defender_signature=$status.AntivirusSignatureVersion
    if ($status.AntivirusSignatureLastUpdated) { $report.defender_signature_updated=$status.AntivirusSignatureLastUpdated.ToUniversalTime().ToString('o') }
  }
  $scanLog=Join-Path $OutputDirectory 'defender.txt'
  $report.defender='scanning'
  try { Run-Checked $scanner @('-Scan','-ScanType','3','-File',$exe,'-DisableRemediation') $scanLog } catch { $report.defender='not-clean';throw }
  $report.defender_scan_exit=0
  $report.defender_output=[IO.File]::ReadAllText($scanLog).Trim()
  if ($report.defender_output -notmatch '(?i)found no threats|no threats found') { $report.defender='unconfirmed';throw 'El analizador no ha confirmado un resultado sin amenazas.' }
  if (-not (Test-Path $exe) -or (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant() -ne $report.sha256) { throw 'El ejecutable no está intacto después del análisis.' }
  $report.defender='no-threats-reported'

  try {
    $previous=Join-Path $OutputDirectory 'Guarda-1.1.0-anterior.exe'
    Invoke-WebRequest 'https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/versiones/1.1.0/Guarda.exe' -OutFile $previous -UseBasicParsing
    $report.previous_sha256=(Get-FileHash $previous -Algorithm SHA256).Hash.ToLowerInvariant()
    $oldScan=& $scanner -Scan -ScanType 3 -File $previous -DisableRemediation 2>&1
    $report.previous_defender_exit=$LASTEXITCODE
    $report.previous_defender_output=($oldScan|Out-String).Trim()
  } catch { $report.previous_scan_note=$_.Exception.Message }

  $report.result='passed'
  Write-Output 'GUARDA_ARTIFACT_BEGIN'
  $b64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($exe))
  for($i=0;$i -lt $b64.Length;$i+=12000) { Write-Output $b64.Substring($i,[Math]::Min(12000,$b64.Length-$i)) }
  Write-Output 'GUARDA_ARTIFACT_END'
  foreach($item in @(@('DARK','ventana.png'),@('LIGHT','ventana-clara.png'),@('UPDATES','actualizaciones.png'))) {
    $imagePath=Join-Path (Join-Path $OutputDirectory 'imagenes') $item[1]
    Write-Output ('GUARDA_IMAGE_'+$item[0]+'_BEGIN')
    $imageBase64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($imagePath))
    for($i=0;$i -lt $imageBase64.Length;$i+=12000) { Write-Output $imageBase64.Substring($i,[Math]::Min(12000,$imageBase64.Length-$i)) }
    Write-Output ('GUARDA_IMAGE_'+$item[0]+'_END')
  }
} catch {
  $report.result='failed';$report.error=$_.Exception.Message
  $report.error_detail=$_.ToString();$report.script_stack=$_.ScriptStackTrace
  throw
} finally {
  $json=$report|ConvertTo-Json -Depth 8 -Compress
  [IO.File]::WriteAllText((Join-Path $OutputDirectory 'validacion-windows.json'),$json,[Text.UTF8Encoding]::new($false))
  Write-Output ('GUARDA_REPORT_JSON:'+ $json)
}
