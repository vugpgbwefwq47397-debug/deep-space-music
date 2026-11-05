using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

// Elog byte substitution documented by dsh-pet-qqmusic (MIT).
// See THIRD-PARTY-LICENSES/dsh-pet-qqmusic.txt. Only playback events survive parsing.
public sealed class NetEaseTimeline {
    readonly string path;
    long offset,anchorTick,position;
    int pid;
    bool playing,anchored;
    string songId="";
    byte[] carry=[];
    DateTime creation;
    static readonly Regex header=new(@"^\[(\d+):\d+:\d{4}/\d{6}:(\d+):",RegexOptions.Compiled);
    static readonly Regex load=new("\\\"songId\\\"\\s*:\\s*\\\"?(\\d+)",RegexOptions.Compiled);
    static readonly Regex seek=new("\\\"setPlayingPosition\\\"\\s*,\\s*(-?\\d+(?:\\.\\d+)?)",RegexOptions.Compiled);
    static readonly Regex status=new("\\\"native[^\\\"]*state\\\"\\s*,\\s*(\\d+)",RegexOptions.Compiled);
    static readonly HttpClient http=new(new HttpClientHandler {UseProxy=false}) {Timeout=TimeSpan.FromSeconds(8)};
    Task<Track?>? metadata;
    string metadataId="";
    long retryAt;
    public record Track(string Id,string Title,string Artist,long Duration);
    public NetEaseTimeline(string? file=null) {path=file??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NetEase","CloudMusic","cloudmusic.elog");}
    public static byte DecodeByte(byte value) {
        int high=value>>4,low=value&15;
        return (byte)(((high^((low+8)%16))*16+(value>>6)*4+((~high)&3))&255);
    }
    void Reset() {songId="";pid=0;position=0;playing=anchored=false;carry=[];metadata=null;metadataId="";}
    public void IngestLine(string line,long nowTick,ISet<int> livePids) {
        var h=header.Match(line);
        if(!h.Success || !int.TryParse(h.Groups[1].Value,out int eventPid) || !livePids.Contains(eventPid) || !long.TryParse(h.Groups[2].Value,out long tick))return;
        // The client logs GetTickCount; map its 32-bit rollover to the current boot.
        if(tick<uint.MaxValue && nowTick>uint.MaxValue)tick+=(nowTick/(1L<<32))*(1L<<32);
        if(tick>nowTick+2000)tick-=1L<<32;
        if(nowTick-tick>12*60*60*1000L || tick>nowTick+2000)return;
        if(line.Contains("\"native",StringComparison.Ordinal) && line.Contains("load",StringComparison.Ordinal)) {
            var id=load.Match(line);
            if(id.Success && id.Groups[1].Length<=20) {songId=id.Groups[1].Value;pid=eventPid;position=0;anchorTick=tick;playing=false;anchored=false;}
            return;
        }
        if(songId.Length==0 || pid!=eventPid || tick<anchorTick)return;
        var move=seek.Match(line);
        if(move.Success && double.TryParse(move.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out double seconds) && double.IsFinite(seconds) && seconds>=0 && seconds<=86400) {
            position=(long)(seconds*1000);anchorTick=tick;anchored=true;return;
        }
        var state=status.Match(line);
        if(state.Success && (state.Groups[1].Value=="1" || state.Groups[1].Value=="2")) {
            if(playing)position+=Math.Max(0,tick-anchorTick);
            playing=state.Groups[1].Value=="1";anchorTick=tick;anchored=true;
        } else if(state.Success) {
            if(playing)position+=Math.Max(0,tick-anchorTick);
            playing=anchored=false;anchorTick=tick;
        }
    }
    void Refresh() {
        try {
            var info=new FileInfo(path);if(!info.Exists){offset=0;Reset();return;}
            bool reset=info.Length<offset || creation!=info.CreationTimeUtc;
            if(reset){offset=0;Reset();creation=info.CreationTimeUtc;}
            if(info.Length==offset)return;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            const int limit=4*1024*1024;
            bool skip=info.Length-offset>limit;
            if(skip){offset=info.Length-limit;Reset();}
            stream.Position=offset;
            byte[] bytes=new byte[(int)Math.Min(limit,stream.Length-offset)];
            int read=stream.Read(bytes,0,bytes.Length);offset+=read;
            byte[] joined=new byte[carry.Length+read];carry.CopyTo(joined,0);Array.Copy(bytes,0,joined,carry.Length,read);
            var live=new HashSet<int>();foreach(var p in Process.GetProcessesByName("cloudmusic"))using(p)live.Add(p.Id);
            int start=0;
            for(int i=0;i<joined.Length;i++)if(DecodeByte(joined[i])==10) {
                if(!skip) {
                    // Decode one bounded line; never save or emit raw log content.
                    if(i-start<=65536) {byte[] decoded=new byte[i-start];for(int n=start;n<i;n++)decoded[n-start]=DecodeByte(joined[n]);IngestLine(Encoding.UTF8.GetString(decoded),Environment.TickCount64,live);}
                } else skip=false;
                start=i+1;
            }
            carry=joined.Length-start<=65536?joined[start..]:[];
        } catch(IOException) {Reset();offset=0;} catch(UnauthorizedAccessException) {Reset();offset=0;}
    }
    public (string Id,long Position,bool Playing,bool Anchored) ReadEvents() {
        Refresh();
        if(pid!=0)try {using var process=Process.GetProcessById(pid);if(!process.ProcessName.Equals("cloudmusic",StringComparison.OrdinalIgnoreCase))Reset();}catch(ArgumentException){Reset();}
        return StateAt(Environment.TickCount64);
    }
    public (string Id,long Position,bool Playing,bool Anchored) StateAt(long tick)=>(songId,Math.Max(0,position+(playing?Math.Max(0,tick-anchorTick):0)),playing,anchored);
    public bool TryApply(Snapshot snapshot) {
        var ev=ReadEvents();if(!ev.Anchored || ev.Id.Length==0 || ev.Playing!=(snapshot.playback=="playing"))return false;
        if(metadata?.IsFaulted==true)_=metadata.Exception;
        if(metadataId!=ev.Id || metadata==null || (metadata.IsCompleted && (!metadata.IsCompletedSuccessfully || metadata.Result==null) && Environment.TickCount64>=retryAt)) {
            metadataId=ev.Id;metadata=LoadTrack(ev.Id);retryAt=Environment.TickCount64+20000;
        }
        var track=metadata.IsCompletedSuccessfully?metadata.Result:null;
        if(track==null || track.Title!=snapshot.title || track.Artist!=snapshot.artist || track.Duration<=0 || ev.Position>track.Duration+1500)return false;
        snapshot.positionMs=Math.Clamp(ev.Position,0,track.Duration);snapshot.durationMs=track.Duration;snapshot.positionConfidence="netease_log";return true;
    }
    static async Task<Track?> LoadTrack(string id) {
        try {
            // Public song metadata only, with no cookies, login or signed audio URL.
            string uri="https://music.163.com/api/song/detail/?id="+id+"&ids=%5B"+id+"%5D";
            using var json=JsonDocument.Parse(await http.GetStringAsync(uri));return ParseTrack(json.RootElement,id);
        } catch(Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException) {return null;}
    }
    public static Track? ParseTrack(JsonElement root,string id) {
        if(!root.TryGetProperty("songs",out var songs) || songs.ValueKind!=JsonValueKind.Array || songs.GetArrayLength()!=1)return null;
        var song=songs[0];
        if(!song.TryGetProperty("id",out var identifier) || identifier.ToString()!=id || !song.TryGetProperty("name",out var name) || name.ValueKind!=JsonValueKind.String || !song.TryGetProperty("duration",out var duration) || duration.ValueKind!=JsonValueKind.Number || !duration.TryGetInt64(out long ms) || ms<=0 || ms>86400000 || !song.TryGetProperty("artists",out var artists) || artists.ValueKind!=JsonValueKind.Array)return null;
        var names=new List<string>();foreach(var artist in artists.EnumerateArray()) {if(artist.ValueKind!=JsonValueKind.Object || !artist.TryGetProperty("name",out var singer) || singer.ValueKind!=JsonValueKind.String)return null;names.Add(singer.GetString()??"");}
        return new(id,name.GetString()??"",string.Join("/",names),ms);
    }
}
