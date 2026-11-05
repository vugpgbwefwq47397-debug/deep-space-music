param([string]$Bridge=(Join-Path $PSScriptRoot '../.research/audit-bridge/MusicBridge.exe'))
$ErrorActionPreference='Stop'
$auditExecutable=(Resolve-Path -LiteralPath $Bridge).Path
$auditRoot=Join-Path (Split-Path $PSScriptRoot) ('.research/audit-ipc-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $auditRoot | Out-Null
$auditPipe='DeepSpaceMusic.audit.'+[Guid]::NewGuid().ToString('N')
$previousPipe=$env:DEEP_SPACE_MUSIC_PIPE;$previousData=$env:DEEP_SPACE_MUSIC_DATA
$script:checks=0;$script:launches=0;$auditProcess=$null
function Check($condition,$message) {if(!$condition){throw $message};$script:checks++;'PASS: '+$message}
function Request($raw) {
 $pipe=[IO.Pipes.NamedPipeClientStream]::new('.',$auditPipe,[IO.Pipes.PipeDirection]::InOut)
 try {
  $pipe.Connect(1500)
  $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true);$writer.AutoFlush=$true
  $reader=[IO.StreamReader]::new($pipe,[Text.UTF8Encoding]::new($false),$false,1024,$true)
  $writer.WriteLine($raw);$pending=$reader.ReadLineAsync()
  if(!$pending.Wait(6000)){throw 'Isolated bridge response timeout'}
  $pending.Result | ConvertFrom-Json
 } finally {if($reader){$reader.Dispose()};if($writer){$writer.Dispose()};$pipe.Dispose()}
}
function Start-AuditBridge {
 $script:launches++
 Start-Process -FilePath $auditExecutable -WorkingDirectory (Split-Path $auditExecutable) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $auditRoot ('out-'+$script:launches+'.log')) -RedirectStandardError (Join-Path $auditRoot ('error-'+$script:launches+'.log'))
}
function Wait-Ready {
 for($i=0;$i -lt 10;$i++) {try {return (Request '{"action":"status"}')} catch {Start-Sleep -Milliseconds 100}}
 throw 'Isolated bridge did not become ready'
}
try {
 $env:DEEP_SPACE_MUSIC_PIPE=$auditPipe;$env:DEEP_SPACE_MUSIC_DATA=$auditRoot
 $auditProcess=Start-AuditBridge
 $initial=Wait-Ready
 Check ($initial.ok -and !$initial.connected) 'clean isolated startup does not select a platform'
 foreach($bad in @('{bad','null','{"protocolVersion":99}','{"action":"connect","platform":"other"}')) {
  $rejected=Request $bad
  Check (!$rejected.ok -and !$rejected.connected) 'malformed or unsupported request is rejected without connecting'
 }
 $selected=Request '{"action":"connect","platform":"qq","autoReconnect":false,"offsetMs":350}'
 Check ($selected.platform -eq 'qq' -and $selected.offsetMs -eq 350) 'explicit connection applies the requested offset'
 foreach($bad in @('{"action":"queue"}','{"action":"playQueue"}','{"action":"command","platform":"netease","command":"pause"}','{"action":"connect","platform":"other"}')) {
  $rejected=Request $bad
  Check (!$rejected.ok -and $rejected.platform -eq 'qq' -and $rejected.offsetMs -eq 350) 'rejected operation preserves the selected platform and timing'
 }
 $configured=Request '{"action":"configure","offsetMs":-2500,"autoReconnect":false}'
 Check ($configured.ok -and $configured.offsetMs -eq -2500) 'negative lyric correction applies immediately'
 $saved=Get-Content (Join-Path $auditRoot 'bridge.json') -Raw | ConvertFrom-Json
 Check ($saved.offsetMs -eq -2500 -and !$saved.autoReconnect) 'lyric correction and reconnect preference are saved together'
 $watch=[Diagnostics.Stopwatch]::StartNew();$healthy=$true
 for($i=0;$i -lt 25;$i++) {$state=Request '{"action":"status"}';$healthy=$healthy -and $state.ok -and $state.offsetMs -eq -2500}
 $watch.Stop();Check ($healthy -and $watch.Elapsed.TotalSeconds -lt 15) '25 consecutive state polls stay healthy after rejected requests'
 $auditProcess.Kill();$auditProcess.WaitForExit(5000) | Out-Null
 $auditProcess=Start-AuditBridge;$restored=Wait-Ready
 Check ($restored.platform -eq 'qq' -and $restored.offsetMs -eq -2500 -and !$restored.connected -and !$restored.autoReconnect) 'restart retains timing and respects disabled auto reconnect'
 $clamped=Request '{"action":"configure","offsetMs":999999,"autoReconnect":false}'
 $saved=Get-Content (Join-Path $auditRoot 'bridge.json') -Raw | ConvertFrom-Json
 Check ($clamped.offsetMs -eq 10000 -and $saved.offsetMs -eq 10000) 'out of range correction is clamped both in memory and on disk'
 $reset=Request '{"action":"disconnect","platform":"未选择","offsetMs":100,"autoReconnect":true}'
 $saved=Get-Content (Join-Path $auditRoot 'bridge.json') -Raw | ConvertFrom-Json
 Check (!$reset.connected -and $reset.platform -eq '' -and $saved.platform -eq '') 'reset clears the saved platform instead of reconnecting to the old client'
 $auditProcess.Kill();$auditProcess.WaitForExit(5000) | Out-Null
 Set-Content -LiteralPath (Join-Path $auditRoot 'bridge.json') -Value '{broken' -Encoding UTF8
 $auditProcess=Start-AuditBridge;$recovered=Wait-Ready
 Check ($recovered.ok -and !$recovered.connected -and $recovered.platform -eq '') 'corrupt configuration recovers to manual platform selection'
 "$script:checks isolated IPC checks passed; no play, pause, skip or client window action dispatched."
} finally {
 if($auditProcess -and !$auditProcess.HasExited){$auditProcess.Kill();$auditProcess.WaitForExit(5000) | Out-Null}
 $env:DEEP_SPACE_MUSIC_PIPE=$previousPipe;$env:DEEP_SPACE_MUSIC_DATA=$previousData
}
