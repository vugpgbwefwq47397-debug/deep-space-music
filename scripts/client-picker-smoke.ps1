param([switch]$OpenClient)
$ErrorActionPreference='Stop'
function Request-Bridge($body) {
 $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','DeepSpaceMusic.v1',[IO.Pipes.PipeDirection]::InOut)
 try {
  $pipe.Connect(3000)
  $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true);$writer.AutoFlush=$true
  $reader=[IO.StreamReader]::new($pipe,[Text.UTF8Encoding]::new($false),$false,1024,$true)
  $writer.WriteLine(($body|ConvertTo-Json -Compress));$pending=$reader.ReadLineAsync()
  if(!$pending.Wait(6000)){throw 'Bridge timeout'}
  $pending.Result|ConvertFrom-Json
 }finally{if($reader){$reader.Dispose()};if($writer){$writer.Dispose()};$pipe.Dispose()}
}
$before=Request-Bridge @{protocolVersion=1;action='status'}
foreach($action in @('queue','playQueue')) {
 $rejected=Request-Bridge @{protocolVersion=1;action=$action;platform='qq';clientVersion=2}
 if($rejected.ok -or $rejected.error -notlike '*游戏内歌单已取消*'){throw "Legacy action still active: $action"}
 if($rejected.connected -ne $before.connected -or $rejected.platform -ne $before.platform){throw 'Rejected action changed media connection'}
}
$wrong=Request-Bridge @{protocolVersion=1;action='openClient';platform='other'}
if($wrong.ok){throw 'Unsupported client accepted'}
if($OpenClient) {
 $opened=Request-Bridge @{protocolVersion=1;action='openClient';platform='qq'}
 if(!$opened.ok -or $opened.result -notlike '*已请求打开 QQ 音乐*'){throw "Client open request failed: $($opened.error)"}
 $opened|ConvertTo-Json -Compress
}
'PASS: old queue and point-play routes rejected, media connection preserved, unsupported client rejected'
