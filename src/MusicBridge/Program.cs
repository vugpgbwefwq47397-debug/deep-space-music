using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Text.Json;
using DeepSpaceMusic;
Console.OutputEncoding=new UTF8Encoding(false);
if(args.Contains("--diagnose")) {Environment.ExitCode=await BridgeDiagnostics.Run();return;}
try {await Run(args);}
catch(Exception e) {
    Console.Error.WriteLine("MusicBridge 启动失败（"+e.GetType().Name+"）："+e.Message);
    Console.Error.WriteLine("请完整解压 Windows x64 包，不要只复制 EXE；保持 MusicBridge 整个文件夹完整且在游戏 Mods 之外。游戏和桥接使用同一 Windows 账号及相同权限。");
    Environment.ExitCode=1;
    if(!Console.IsInputRedirected) {Console.Error.WriteLine("按回车关闭窗口。");Console.ReadLine();}
}
static async Task Run(string[] args) {
var json=new JsonSerializerOptions {IncludeFields=true};
string root=Environment.GetEnvironmentVariable("DEEP_SPACE_MUSIC_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSpaceMusic");
Directory.CreateDirectory(root);
var automaticLyrics=new AutomaticLyrics(new LocalLyricsProvider(Path.Combine(root,"Lyrics")));
using var sessions=new Sessions(automaticLyrics,root);
if(args.Length==4 && args[0]=="--lyrics-check") {var list=automaticLyrics.Get(args[1],args[2],long.Parse(args[3]));for(int attempt=0;attempt<20 && automaticLyrics.Status=="loading_lrclib";attempt++){await Task.Delay(500);list=automaticLyrics.Get(args[1],args[2],long.Parse(args[3]));}Console.WriteLine(JsonSerializer.Serialize(new {source=automaticLyrics.Status,lineCount=list.Count}));return;}

var config=Path.Combine(root,"bridge.json");
if(File.Exists(config)) {try {var saved=JsonSerializer.Deserialize<Request>(File.ReadAllText(config),json); if(saved!=null && MusicPlatforms.IsSupported(saved.platform)) {sessions.Platform=saved.platform;sessions.Offset=Math.Clamp(saved.offsetMs,-10000,10000);sessions.RestoreConnection(saved.autoReconnect,saved.connectionEnabled);}} catch(JsonException) {Console.Error.WriteLine("配置损坏，请重新选择平台");}}
if(args.Contains("--probe")) {foreach(var p in MusicPlatforms.Ids) {sessions.Disconnect(); sessions.Platform=p;sessions.WantConnection=true;try{Console.WriteLine(JsonSerializer.Serialize(await sessions.Read(),json));}catch(Exception e){Console.WriteLine(e.Message);}} return;}
if(args.Contains("--timeline")){var manager=await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();foreach(var s in manager.GetSessions().Where(s=>Sessions.Matches("qq",s.SourceAppUserModelId))){var t=s.GetTimelineProperties();Console.WriteLine(JsonSerializer.Serialize(new {position=t.Position.TotalMilliseconds,end=t.EndTime.TotalMilliseconds,updated=t.LastUpdatedTime,now=DateTimeOffset.UtcNow},json));}return;}
if(args.Length==2 && args[0]=="--watch") {if(!MusicPlatforms.IsSupported(args[1]))throw new InvalidDataException("未知平台");sessions.Platform=args[1];sessions.WantConnection=true;for(int i=0;i<20;i++){Console.WriteLine(JsonSerializer.Serialize(await sessions.Read(),json));await Task.Delay(500);}return;}

if(args.Length==3 && args[0]=="--command") {sessions.Platform=args[1];sessions.WantConnection=true;var result=await sessions.Control(args[2]);Console.WriteLine(JsonSerializer.Serialize(new {result.ok,result.result,state=await sessions.Read()},json));return;}
string pipeName=Environment.GetEnvironmentVariable("DEEP_SPACE_MUSIC_PIPE") ?? "DeepSpaceMusic.v1";
if(pipeName.Length>64 || pipeName.Length==0 || pipeName.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))throw new InvalidDataException("无效的本地管道名称");
using var singleton=new Mutex(true,pipeName=="DeepSpaceMusic.v1"?"Local\\DeepSpaceMusicBridge":"Local\\"+pipeName,out bool created);
if(!created) {Console.Error.WriteLine("桥接服务已在运行");return;}
Console.WriteLine("沉浸歌词 MusicBridge 0.2.7 | 本机当前用户命名管道 | Ctrl+C 退出\n本地歌词: "+Path.Combine(root,"Lyrics"));
Console.WriteLine("请保持本窗口开启；游戏中 F9 / F10 → 连接与工具选择平台。SMTC 与本地桥接是两层连接。");
using var stop=new CancellationTokenSource();
Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;stop.Cancel();};
bool announced=false;
while(!stop.IsCancellationRequested) {
    // Windows validates the connecting user's SID; no TCP listener, browser origin or bearer token.
    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
    if(!announced) {Console.WriteLine("本地管道已就绪："+pipeName+"。等待游戏连接。");announced=true;}
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
                    if(!MusicPlatforms.IsSupported(req.platform)) throw new InvalidDataException("请先选择网易云音乐、QQ 音乐或酷狗音乐");
                    sessions.Disconnect();sessions.Platform=req.platform;sessions.AutoReconnect=req.autoReconnect;sessions.Offset=Math.Clamp(req.offsetMs,-10000,10000);sessions.WantConnection=true;
                    
                    req.offsetMs=sessions.Offset;req.connectionEnabled=true;File.WriteAllText(config,JsonSerializer.Serialize(req,json));break;
                case "disconnect":
                    if(!string.IsNullOrEmpty(req.platform) && req.platform!="未选择" && req.platform!=sessions.Platform)throw new InvalidDataException("断开平台与当前连接不一致，请重新检测");
                    sessions.Disconnect(manual:true);
                    if(req.platform=="未选择") {
                        sessions.Platform="";sessions.Offset=Math.Clamp(req.offsetMs,-10000,10000);sessions.AutoReconnect=req.autoReconnect;
                    }
                    File.WriteAllText(config,JsonSerializer.Serialize(new Request {platform=sessions.Platform,offsetMs=sessions.Offset,autoReconnect=sessions.AutoReconnect,connectionEnabled=false},json));
                    break;
                case "openClient":
                    if(req.platform!=sessions.Platform)throw new InvalidDataException("选歌平台与当前连接不一致，请重新连接");
                    result=req.platform=="local"?sessions.Local.PickFolder():ClientPanel.Open(req.platform);break;
                case "queue":
                case "playQueue":
                    throw new InvalidOperationException("游戏内歌单已取消，请双击磁带并点击打开所选音乐客户端选歌");
                case "configure":
                    int offset=Math.Clamp(req.offsetMs,-10000,10000);
                    if(sessions.AutoReconnect!=req.autoReconnect || sessions.Offset!=offset) {
                        sessions.AutoReconnect=req.autoReconnect;sessions.Offset=offset;
                        if(MusicPlatforms.IsSupported(sessions.Platform)) File.WriteAllText(config,JsonSerializer.Serialize(new Request {platform=sessions.Platform,autoReconnect=sessions.AutoReconnect,offsetMs=sessions.Offset,connectionEnabled=sessions.ConnectionEnabled},json));
                    }
                    break;
                case "reload":sessions.ReloadLyrics();break;
                case "command":
                    if(req.platform!=sessions.Platform) throw new InvalidDataException("指令平台与当前连接不一致，请重新连接");
                    (ok,result)=await sessions.Control(req.command,timeout.Token);break;
                default:throw new InvalidDataException("未知操作");
            }
            state=await sessions.Read(timeout.Token);state.ok=ok;state.result=req.action=="reload"?LyricReloadFeedback.Describe(state):result;
        } catch(Exception e) {
            // A rejected request preserves the current media session and lyrics.
            try {state=await sessions.Read(timeout.Token);}
            catch {state=new Snapshot();}
            state.ok=false;state.error=e.Message;
        }
        await writer.WriteLineAsync(JsonSerializer.Serialize(state,json).AsMemory(),timeout.Token);
    } catch(OperationCanceledException) {} catch(Exception e) {Console.Error.WriteLine(DateTime.Now.ToString("s")+" "+e.Message);}
}
}
