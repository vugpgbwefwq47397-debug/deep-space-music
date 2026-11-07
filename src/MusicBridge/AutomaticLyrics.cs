using System.Collections.Concurrent;
using System.Text.Json;
namespace DeepSpaceMusic;
// Local files take priority; public LRCLIB lookup runs outside the IPC path.
public sealed class AutomaticLyrics : ILyricsProvider {
    readonly ILyricsProvider local;
    readonly Func<string,string,long,Task<List<LyricLine>>>? fetch;
    readonly Func<DateTime> clock;
    public record Lookup(List<LyricLine> Lines,string Source,string Detail="");
    readonly ConcurrentDictionary<string,Task<Lookup>> jobs=new();
    readonly ConcurrentDictionary<string,DateTime> attempts=new();
    readonly ConcurrentDictionary<string,(DateTime time,List<LyricLine> lines)> localCache=new();
    public string Status {get;private set;}="unavailable";
    public string Platform {get;set;}="";
    public string NetEaseSongId {get;set;}="";
    public string Source {get;private set;}="";
    public string Diagnostic {get;private set;}="";
    public AutomaticLyrics(ILyricsProvider local,Func<string,string,long,Task<List<LyricLine>>>? fetch=null,Func<DateTime>? clock=null) {
        this.local=local;this.fetch=fetch;this.clock=clock??(()=>DateTime.UtcNow);
    }
    public void Reset(){jobs.Clear();attempts.Clear();localCache.Clear();Status="unavailable";Source=Diagnostic="";}
    public List<LyricLine> Get(string title,string artist,long durationMs) {
        string platform=Platform,id=platform=="netease"?NetEaseSongId:"";
        string key=platform+"|"+id+"|"+title+"|"+artist+"|"+durationMs;
        var now=clock();
        Source=Diagnostic="";
        if(!localCache.TryGetValue(key,out var cached) || now<cached.time || now-cached.time>=TimeSpan.FromSeconds(2)) {
            if(localCache.Count>=128)localCache.Clear();
            cached=(now,local.Get(title,artist,durationMs));localCache[key]=cached;
        }
        if(cached.lines.Count>0){Status="local";Source="本地 LRC";return cached.lines;}
        if(durationMs<=0 || title.Trim().Length==0 || artist.Trim().Length==0){Status="waiting_for_metadata";Diagnostic=title.Trim().Length==0 || artist.Trim().Length==0?"客户端尚未提供完整歌名和歌手，暂不猜测同名歌曲。":"客户端尚未提供歌曲时长，请先查看播放进度提示；重新匹配不会生成缺失的时钟。";return [];}
        if(jobs.TryGetValue(key,out var previous) && previous.IsCompleted && (!previous.IsCompletedSuccessfully || previous.Result.Lines.Count==0) && attempts.TryGetValue(key,out var time) && (now<time || now-time>=TimeSpan.FromSeconds(20)))jobs.TryRemove(key,out _);
        if(jobs.Count>=128){jobs.Clear();attempts.Clear();}
        var task=jobs.GetOrAdd(key,_=>{attempts[key]=now;return fetch!=null?Injected(title,artist,durationMs):FetchForPlatform(platform,id,title,artist,durationMs);});
        Status=!task.IsCompleted?"loading_lrclib":!task.IsCompletedSuccessfully?"lyrics_lookup_failed":task.Result.Lines.Count>0?"online":task.Result.Detail.Length>0?"lyrics_lookup_failed":"no_matching_lyrics";
        if(task.IsCompletedSuccessfully) {Source=task.Result.Source;Diagnostic=task.Result.Detail;}
        else if(task.IsFaulted)Diagnostic="歌词服务请求失败；检查网络或稍后重试";
        if(task.IsFaulted)_=task.Exception; // Observe failed background jobs; the next retry can recover.
        return task.IsCompletedSuccessfully?task.Result.Lines:[];
    }
    async Task<Lookup> Injected(string title,string artist,long ms)=>new(await fetch!(title,artist,ms),"online");
    public static bool Matches(string title,string artist,long durationMs,string candidateTitle,string candidateArtist,double seconds)=>
        MetadataMatch.Recording(title,artist,durationMs,candidateTitle,candidateArtist,seconds);
    static async Task<Lookup> Safe(string source,Func<Task<List<LyricLine>>> request) {
        try {return new(await request(),source);}
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or JsonException or FormatException) {
            return new([],source,source+(e is OperationCanceledException?"：请求超时":"：网络或响应异常"));
        }
    }
    static Task<Lookup> FetchForPlatform(string platform,string id,string title,string artist,long ms) {
        var requests=new List<Task<Lookup>>();
        if(platform=="netease" && id.Length>0)requests.Add(Safe("网易云",()=>NetEaseLyrics.Fetch(id)));
        else requests.Add(Safe("网易云曲库",()=>NetEaseLyrics.Search(title,artist,ms)));
        requests.Add(Safe("酷狗",()=>KugouLyrics.Fetch(title,artist,ms)));
        requests.Add(Safe("LRCLIB",()=>Fetch(title,artist,ms)));
        return FirstMatch(requests);
    }
    public static async Task<Lookup> FirstMatch(IEnumerable<Task<Lookup>> requests) {
        var pending=requests.ToList();var errors=new List<string>();
        while(pending.Count>0) {
            var completed=await Task.WhenAny(pending);pending.Remove(completed);
            var result=await completed;
            if(result.Lines.Count>0)return result;
            if(result.Detail.Length>0)errors.Add(result.Detail);
        }
        return new([],"",string.Join("；",errors));
    }
    static async Task<List<LyricLine>> Fetch(string title,string artist,long ms) {
            using var json=await LyricsHttp.Read("https://lrclib.net/api/search?track_name="+Uri.EscapeDataString(title)+"&artist_name="+Uri.EscapeDataString(artist));
            return ParseResponse(title,artist,ms,json.RootElement);
    }
    public static List<LyricLine> ParseResponse(string title,string artist,long ms,JsonElement root) {
        if(root.ValueKind!=JsonValueKind.Array)throw new JsonException("歌词接口返回格式错误");
        List<LyricLine> best=[];double bestDifference=double.MaxValue;
        foreach(var row in root.EnumerateArray()) {
            if(row.ValueKind!=JsonValueKind.Object || !Text(row,"syncedLyrics",out var text) || !Text(row,"trackName",out var name) || !Text(row,"artistName",out var singer) || !row.TryGetProperty("duration",out var duration) || duration.ValueKind!=JsonValueKind.Number || !duration.TryGetDouble(out double seconds) || !double.IsFinite(seconds))continue;
            if(!Matches(title,artist,ms,name,singer,seconds))continue;
            double difference=Math.Abs(seconds*1000-ms);if(difference>=bestDifference)continue;
            var parsed=Lrc.Parse(text);if(parsed.Count==0)continue;
            best=parsed;bestDifference=difference;
        }
        return best;
    }
    static bool Text(JsonElement row,string property,out string text) {
        text="";
        if(!row.TryGetProperty(property,out var value) || value.ValueKind!=JsonValueKind.String)return false;
        text=value.GetString()??"";return text.Length>0;
    }
}
