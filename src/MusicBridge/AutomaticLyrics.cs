using System.Collections.Concurrent;
using System.Text.Json;
namespace DeepSpaceMusic;
// Local files take priority; public LRCLIB lookup runs outside the IPC path.
public sealed class AutomaticLyrics : ILyricsProvider {
    readonly ILyricsProvider local;
    readonly Func<string,string,long,Task<List<LyricLine>>>? fetch;
    readonly Func<DateTime> clock;
    readonly ConcurrentDictionary<string,Task<List<LyricLine>>> jobs=new();
    readonly ConcurrentDictionary<string,DateTime> attempts=new();
    readonly ConcurrentDictionary<string,(DateTime time,List<LyricLine> lines)> localCache=new();
    static readonly HttpClient http=CreateClient();
    public string Status {get;private set;}="unavailable";
    public string Platform {get;set;}="";
    public AutomaticLyrics(ILyricsProvider local,Func<string,string,long,Task<List<LyricLine>>>? fetch=null,Func<DateTime>? clock=null) {
        this.local=local;this.fetch=fetch;this.clock=clock??(()=>DateTime.UtcNow);
    }
    public void Reset(){jobs.Clear();attempts.Clear();localCache.Clear();}
    static HttpClient CreateClient() {var h=new HttpClient(new HttpClientHandler {UseProxy=false}) {Timeout=TimeSpan.FromSeconds(8)};h.DefaultRequestHeaders.UserAgent.ParseAdd("DeepSpaceMusic/0.1");return h;}
    public List<LyricLine> Get(string title,string artist,long durationMs) {
        string key=Platform+"|"+title+"|"+artist+"|"+durationMs;
        var now=clock();
        if(!localCache.TryGetValue(key,out var cached) || now<cached.time || now-cached.time>=TimeSpan.FromSeconds(2)) {
            if(localCache.Count>=128)localCache.Clear();
            cached=(now,local.Get(title,artist,durationMs));localCache[key]=cached;
        }
        if(cached.lines.Count>0){Status="local";return cached.lines;}
        if(durationMs<=0 || title.Length==0 || artist.Length==0){Status="waiting_for_metadata";return [];}
        if(jobs.TryGetValue(key,out var previous) && previous.IsCompleted && (!previous.IsCompletedSuccessfully || previous.Result.Count==0) && attempts.TryGetValue(key,out var time) && (now<time || now-time>=TimeSpan.FromSeconds(20)))jobs.TryRemove(key,out _);
        if(jobs.Count>=128){jobs.Clear();attempts.Clear();}
        var task=jobs.GetOrAdd(key,_=>{attempts[key]=now;return fetch!=null?fetch(title,artist,durationMs):FetchForPlatform(Platform,title,artist,durationMs);});
        Status=!task.IsCompleted?"loading_lrclib":!task.IsCompletedSuccessfully?"lyrics_lookup_failed":task.Result.Count>0?(Platform=="kugou"?"online":"lrclib"):"no_matching_lyrics";
        if(task.IsFaulted)_=task.Exception; // Observe failed background jobs; the next retry can recover.
        return task.IsCompletedSuccessfully?task.Result:[];
    }
    public static bool Matches(string title,string artist,long durationMs,string candidateTitle,string candidateArtist,double seconds)=>
        string.Equals(title.Trim(),candidateTitle.Trim(),StringComparison.OrdinalIgnoreCase) && string.Equals(artist.Trim(),candidateArtist.Trim(),StringComparison.OrdinalIgnoreCase) && durationMs>0 && Math.Abs(durationMs-seconds*1000)<=2000;
    static async Task<List<LyricLine>> FetchForPlatform(string platform,string title,string artist,long ms) {
        if(platform=="kugou") {
            try {var native=await KugouLyrics.Fetch(title,artist,ms);if(native.Count>0)return native;}
            catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException) { }
        }
        return await Fetch(title,artist,ms);
    }
    static async Task<List<LyricLine>> Fetch(string title,string artist,long ms) {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var response=await http.GetAsync("https://lrclib.net/api/search?track_name="+Uri.EscapeDataString(title)+"&artist_name="+Uri.EscapeDataString(artist),HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            response.EnsureSuccessStatusCode();await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
            using var limited=new MemoryStream();var buffer=new byte[8192];int n;
            while((n=await stream.ReadAsync(buffer,timeout.Token))>0) {if(limited.Length+n>2_000_000)return [];limited.Write(buffer,0,n);}
            using var json=JsonDocument.Parse(limited.ToArray());
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
