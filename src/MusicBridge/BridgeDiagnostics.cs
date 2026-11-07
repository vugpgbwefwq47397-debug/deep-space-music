using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Windows.Media.Control;
namespace DeepSpaceMusic;

public static class BridgeDiagnostics {
    public static async Task<int> Run() {
        Console.WriteLine("沉浸歌词 MusicBridge 0.2.5 只读诊断（不播放、不切歌、不读取账号凭据）");
        Console.WriteLine("Windows: "+Environment.OSVersion.VersionString+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        try {
            using var pipe=new NamedPipeClientStream(".","DeepSpaceMusic.v1",PipeDirection.InOut,PipeOptions.Asynchronous);
            using var timeout=new CancellationTokenSource(4500);
            await pipe.ConnectAsync(1500,timeout.Token);
            using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
            using var reader=new StreamReader(pipe,new UTF8Encoding(false),false,1024,true);
            await writer.WriteLineAsync("{\"protocolVersion\":1,\"action\":\"status\"}");
            string response=await reader.ReadLineAsync(timeout.Token)??throw new IOException("桥接没有响应");
            var state=JsonSerializer.Deserialize<Snapshot>(response,new JsonSerializerOptions {IncludeFields=true})??throw new JsonException("状态为空");
            Console.WriteLine("本地管道：已连通；桥接版本 "+(state.bridgeVersion.Length>0?state.bridgeVersion:"0.2.1 或更早"));
            Console.WriteLine("平台："+state.platform+"；媒体会话："+(state.connected?"已连接":"未连接")+"；歌词状态："+state.lyricsState);
            Console.WriteLine("播放进度："+state.positionConfidence+"；时长："+state.durationMs/1000+" 秒；歌词来源："+state.lyricsSource);
            if(state.error.Length>0)Console.WriteLine("连接提示："+state.error);
            if(state.timelineDetail.Length>0)Console.WriteLine("进度提示："+state.timelineDetail);
            if(state.lyricsDetail.Length>0)Console.WriteLine("歌词提示："+state.lyricsDetail);
        } catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException or JsonException) {
            Console.WriteLine("本地管道：未连通（"+e.GetType().Name+"）");
            Console.WriteLine("先运行 MusicBridge.exe 并保持开启。工坊订阅和 SMTC 都不会自动启动桥接。游戏与桥接请使用同一 Windows 账号、相同权限，建议均不以管理员运行。");
        }
        try {
            var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var ids=manager.GetSessions().Select(s=>s.SourceAppUserModelId).ToArray();
            foreach(string platform in MusicPlatforms.Ids)Console.WriteLine(MusicPlatforms.Name(platform)+" SMTC："+(ids.Any(id=>Sessions.Matches(platform,id))?"已检测到":"未检测到；请在客户端开启系统媒体控件并播放一首歌"));
            Console.WriteLine("SMTC 检测与本地管道是两层连接，请分别检查上面的结果。");
            return 0;
        } catch(Exception e) {Console.WriteLine("SMTC 访问失败（"+e.GetType().Name+"），请检查 Windows 媒体会话环境。");return 1;}
    }
}
