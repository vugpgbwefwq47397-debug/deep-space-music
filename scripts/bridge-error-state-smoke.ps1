$ErrorActionPreference='Stop'
function Request-Bridge($Body) {
 $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','DeepSpaceMusic.v1',[IO.Pipes.PipeDirection]::InOut)
 try {
  $pipe.Connect(3000)
  $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true);$writer.AutoFlush=$true
  $reader=[IO.StreamReader]::new($pipe,[Text.UTF8Encoding]::new($false),$false,1024,$true)
  $writer.WriteLine(($Body | ConvertTo-Json -Compress))
  $pending=$reader.ReadLineAsync();if(!$pending.Wait(5000)){throw 'Bridge timeout'}
  $pending.Result | ConvertFrom-Json
 } finally {if($reader){$reader.Dispose()};if($writer){$writer.Dispose()};$pipe.Dispose()}
}
$before=Request-Bridge @{protocolVersion=1;clientVersion=2;action='status'}
foreach($action in @('queue','playQueue')) {
 $rejected=Request-Bridge @{protocolVersion=1;clientVersion=2;action=$action;platform=$before.platform}
 if($rejected.ok -or $rejected.error -notlike '*游戏内歌单已取消*'){throw 'Retired queue action was not rejected'}
 if($rejected.connected -ne $before.connected -or $rejected.platform -ne $before.platform -or $rejected.offsetMs -ne $before.offsetMs){throw 'Rejected action changed connection or lyric correction'}
}
$after=Request-Bridge @{protocolVersion=1;clientVersion=2;action='status'}
if(!$after.ok -or $after.connected -ne $before.connected -or $after.platform -ne $before.platform){throw 'Status did not recover after rejected action'}
'PASS: retired queue routes rejected; connection and lyric correction preserved; subsequent status healthy'
