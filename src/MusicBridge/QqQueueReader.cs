using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace DeepSpaceMusic;
public sealed class QueueReadResult {
    public bool ok,foregroundUnchanged;
    public string error="",sourceVersion="";
    public int total;
    public long elapsedMs;
    public QueueTrack[] tracks=[];
}
public static class QqQueueReader {
    public static string DatabasePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Tencent","QQMusic","qmlist64.db");
    public static string Stamp() {
        var file=new FileInfo(DatabasePath);var journal=new FileInfo(DatabasePath+"-journal");
        return file.Exists?$"{file.Length}:{file.LastWriteTimeUtc.Ticks}:{(journal.Exists?journal.Length:0)}:{(journal.Exists?journal.LastWriteTimeUtc.Ticks:0)}":"missing";
    }
    public static QueueTrack[] Validate(QueueReadResult result) {
        if(!result.ok)throw new InvalidDataException(result.error.Length>0?result.error:"后台队列读取失败");
        if(result.tracks==null || result.total!=result.tracks.Length || result.total>10000 || result.total<0)
            throw new InvalidDataException("后台队列数量校验失败");
        if(result.tracks.Any(x=>x==null || string.IsNullOrEmpty(x.title) || x.artist==null || x.title.Length>1024 || x.artist.Length>1024))
            throw new InvalidDataException("后台队列曲目信息校验失败");
        return result.tracks;
    }
    public static async Task<QueueTrack[]> Read() {
        string helper=Path.Combine(AppContext.BaseDirectory,"QueueReader.exe");
        if(!File.Exists(helper))throw new FileNotFoundException("后台队列读取组件缺失，请更新整个 MusicBridge 文件夹");
        var start=new ProcessStartInfo(helper) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        // Process metadata only; never inspect or operate client windows.
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var p in Process.GetProcessesByName("QQMusic"))using(p) {try {if(p.MainModule?.FileName is string path)paths.Add(Path.GetDirectoryName(path)!);}catch(System.ComponentModel.Win32Exception) {}}
        if(paths.Count>1)throw new InvalidOperationException("检测到不同路径的 QQ 客户端，无法唯一核对队列");
        if(paths.Count==1)start.ArgumentList.Add(paths.Single());
        using var process=new Process {StartInfo=start};
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
        try {
            process.Start();
            var output=ReadBounded(process.StandardOutput,8*1024*1024,timeout.Token);
            var errors=ReadBounded(process.StandardError,4096,timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            string payload=await output;await errors;
            if(string.IsNullOrWhiteSpace(payload))throw new IOException("QQ 后台读取组件异常退出，请检查客户端版本后重试");
            var result=JsonSerializer.Deserialize<QueueReadResult>(payload,new JsonSerializerOptions {IncludeFields=true})??throw new InvalidDataException("后台队列响应为空");
            return Validate(result);
        }catch(OperationCanceledException) {throw new TimeoutException("后台队列读取超时，请稍后刷新；音乐客户端不会被打开");}
        catch(JsonException) {throw new InvalidDataException("QQ 后台读取组件响应无效，请更新完整桥接文件夹");}
        finally {try {if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException) {}}
    }
    static async Task<string> ReadBounded(StreamReader reader,int limit,CancellationToken token) {
        var result=new StringBuilder();var buffer=new char[4096];int count;
        while((count=await reader.ReadAsync(buffer,token))>0) {if(result.Length+count>limit)throw new InvalidDataException("后台队列响应过长");result.Append(buffer,0,count);}
        return result.ToString();
    }
}
