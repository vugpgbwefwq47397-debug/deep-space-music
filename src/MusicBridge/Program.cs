using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Text.Json;
using DeepSpaceMusic;
var json=new JsonSerializerOptions {IncludeFields=true};
string root=Environment.GetEnvironmentVariable("DEEP_SPACE_MUSIC_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSpaceMusic");
Directory.CreateDirectory(root);
var automaticLyrics=new AutomaticLyrics(new LocalLyricsProvider(Path.Combine(root,"Lyrics")));
var sessions=new Sessions(automaticLyrics);
if(args.Length==4 && args[0]=="--lyrics-check") {var list=automaticLyrics.Get(args[1],args[2],long.Parse(args[3]));for(int attempt=0;attempt<20 && automaticLyrics.Status=="loading_lrclib";attempt++){await Task.Delay(500);list=automaticLyrics.Get(args[1],args[2],long.Parse(args[3]));}Console.WriteLine(JsonSerializer.Serialize(new {source=automaticLyrics.Status,lineCount=list.Count}));return;}

var config=Path.Combine(root,"bridge.json");
if(File.Exists(config)) {try {var saved=JsonSerializer.Deserialize<Request>(File.ReadAllText(config),json); if(saved!=null && saved.platform is "qq" or "netease") {sessions.Platform=saved.platform;sessions.AutoReconnect=saved.autoReconnect;sessions.Offset=Math.Clamp(saved.offsetMs,-10000,10000);sessions.WantConnection=saved.autoReconnect;}} catch(JsonException) {Console.Error.WriteLine("配置损坏，请重新选择平台");}}
if(args.Contains("--probe")) {foreach(var p in new[]{"netease","qq"}) {sessions.Disconnect(); sessions.Platform=p;sessions.WantConnection=true;try{Console.WriteLine(JsonSerializer.Serialize(await sessions.Read(),json));}catch(Exception e){Console.WriteLine(e.Message);}} return;}
if(args.Contains("--timeline")){var manager=await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();foreach(var s in manager.GetSessions().Where(s=>Sessions.Matches("qq",s.SourceAppUserModelId))){var t=s.GetTimelineProperties();Console.WriteLine(JsonSerializer.Serialize(new {position=t.Position.TotalMilliseconds,end=t.EndTime.TotalMilliseconds,updated=t.LastUpdatedTime,now=DateTimeOffset.UtcNow},json));}return;}

if(args.Length==3 && args[0]=="--command") {sessions.Platform=args[1];sessions.WantConnection=true;var result=await sessions.Control(args[2]);Console.WriteLine(JsonSerializer.Serialize(new {result.ok,result.result,state=await sessions.Read()},json));return;}
string pipeName=Environment.GetEnvironmentVariable("DEEP_SPACE_MUSIC_PIPE") ?? "DeepSpaceMusic.v1";
if(pipeName.Length>64 || pipeName.Length==0 || pipeName.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))throw new InvalidDataException("无效的本地管道名称");
using var singleton=new Mutex(true,pipeName=="DeepSpaceMusic.v1"?"Local\\DeepSpaceMusicBridge":"Local\\"+pipeName,out bool created);
if(!created) {Console.Error.WriteLine("桥接服务已在运行");return;}
Console.WriteLine("Deep Space MusicBridge 0.1 | 本机当前用户命名管道 | Ctrl+C 退出\n本地歌词: "+Path.Combine(root,"Lyrics"));
using var stop=new CancellationTokenSource();
Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;stop.Cancel();};
while(!stop.IsCancellationRequested) {
    // Windows validates the connecting user's SID; no TCP listener, browser origin or bearer token.
    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
    try {
        await pipe.WaitForConnectionAsync(stop.Token);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(4000);
        using var reader=new StreamReader(pipe,new UTF8Encoding(false),false,1024,true);
        using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
        var buffer=new StringBuilder();
        while(true) {var ch=new char[1]; if(await reader.ReadAsync(ch,timeout.Token)==0) break; if(ch[0]=='\n') break; if(buffer.Length>=8192) throw new InvalidDataException("请求过长");buffer.Append(ch[0]);}
        Snapshot state;
        try {
            var req=JsonSerializer.Deserialize<Request>(buffer.ToString(),json)??throw new InvalidDataException("空请求");
            if(req.protocolVersion!=1) throw new InvalidDataException("协议版本不支持");
            bool ok=true; string result="";
            switch(req.action) {
                case "status": break;
                case "connect":
                    if(req.platform is not ("qq" or "netease")) throw new InvalidDataException("请先选择网易云音乐或 QQ 音乐");
                    sessions.Disconnect();sessions.Platform=req.platform;sessions.AutoReconnect=req.autoReconnect;sessions.Offset=Math.Clamp(req.offsetMs,-10000,10000);sessions.WantConnection=true;
                    
                    req.offsetMs=sessions.Offset;File.WriteAllText(config,JsonSerializer.Serialize(req,json));break;
                case "disconnect":
                    sessions.Disconnect();
                    if(req.platform=="未选择") {
                        sessions.Platform="";sessions.Offset=Math.Clamp(req.offsetMs,-10000,10000);sessions.AutoReconnect=req.autoReconnect;
                        File.WriteAllText(config,JsonSerializer.Serialize(new Request {platform="",offsetMs=sessions.Offset,autoReconnect=sessions.AutoReconnect},json));
                    }
                    break;
                case "openClient":
                    result=ClientPanel.Open(req.platform);break;
                case "queue":
                case "playQueue":
                    throw new InvalidOperationException("游戏内歌单已取消，请双击磁带并点击打开 QQ 音乐选歌");
                case "configure":
                    int offset=Math.Clamp(req.offsetMs,-10000,10000);
                    if(sessions.AutoReconnect!=req.autoReconnect || sessions.Offset!=offset) {
                        sessions.AutoReconnect=req.autoReconnect;sessions.Offset=offset;
                        if(sessions.Platform is "qq" or "netease") File.WriteAllText(config,JsonSerializer.Serialize(new Request {platform=sessions.Platform,autoReconnect=sessions.AutoReconnect,offsetMs=sessions.Offset},json));
                    }
                    break;
                case "reload":sessions.ReloadLyrics();break;
                case "command":
                    if(req.platform!=sessions.Platform) throw new InvalidDataException("指令平台与当前连接不一致，请重新连接");
                    (ok,result)=await sessions.Control(req.command,timeout.Token);break;
                default:throw new InvalidDataException("未知操作");
            }
            state=await sessions.Read(timeout.Token);state.ok=ok;state.result=result;
        } catch(Exception e) {
            // A rejected request preserves the current media session and lyrics.
            try {state=await sessions.Read(timeout.Token);}
            catch {state=new Snapshot();}
            state.ok=false;state.error=e.Message;
        }
        await writer.WriteLineAsync(JsonSerializer.Serialize(state,json).AsMemory(),timeout.Token);
    } catch(OperationCanceledException) {} catch(Exception e) {Console.Error.WriteLine(DateTime.Now.ToString("s")+" "+e.Message);}
}
