using System.Text.Json;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

public static class NetEaseLyrics {
    public static async Task<List<LyricLine>> Fetch(string verifiedSongId,CancellationToken token=default) {
        if(!Regex.IsMatch(verifiedSongId,@"^\d{1,20}$"))return [];
        using var json=await LyricsHttp.Read("https://music.163.com/api/song/lyric?id="+verifiedSongId+"&lv=-1&kv=-1&tv=-1",token);
        return Parse(json.RootElement);
    }
    public static async Task<List<LyricLine>> Search(string title,string artist,long duration) {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var json=await LyricsHttp.Read("https://music.163.com/api/search/get/web?s="+Uri.EscapeDataString(title+" "+artist)+"&type=1&offset=0&limit=20",timeout.Token);
        EnsureResponse(json.RootElement);
        foreach(string id in Candidates(json.RootElement,title,artist,duration).Take(3)) {
            var lines=await Fetch(id,timeout.Token);if(lines.Count>0)return lines;
        }
        return [];
    }
    public static List<string> Candidates(JsonElement root,string title,string artist,long duration) {
        var found=new List<(string Id,long Difference)>();
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("result",out var result) || result.ValueKind!=JsonValueKind.Object ||
            !result.TryGetProperty("songs",out var songs) || songs.ValueKind!=JsonValueKind.Array)return [];
        foreach(var song in songs.EnumerateArray()) {
            if(song.ValueKind!=JsonValueKind.Object || !song.TryGetProperty("id",out var id) || !Regex.IsMatch(id.ToString(),@"^\d{1,20}$") ||
                !song.TryGetProperty("name",out var name) || name.ValueKind!=JsonValueKind.String || !song.TryGetProperty("duration",out var time) || time.ValueKind!=JsonValueKind.Number ||
                !time.TryGetInt64(out long ms) || !song.TryGetProperty("artists",out var artists) || artists.ValueKind!=JsonValueKind.Array)continue;
            var names=new List<string>();bool valid=true;
            foreach(var singer in artists.EnumerateArray()) {
                if(singer.ValueKind!=JsonValueKind.Object || !singer.TryGetProperty("name",out var text) || text.ValueKind!=JsonValueKind.String || text.GetString()?.Length==0){valid=false;break;}
                names.Add(text.GetString()??"");
            }
            if(valid && names.Count>0 && MetadataMatch.Recording(title,artist,duration,name.GetString()??"",string.Join("/",names),ms/1000d))found.Add((id.ToString(),Math.Abs(ms-duration)));
        }
        return found.OrderBy(x=>x.Difference).Select(x=>x.Id).Distinct().ToList();
    }
    public static List<LyricLine> Parse(JsonElement root) {
        EnsureResponse(root);
        if(!root.TryGetProperty("lrc",out var lrc) || lrc.ValueKind!=JsonValueKind.Object || !lrc.TryGetProperty("lyric",out var text) || text.ValueKind!=JsonValueKind.String)return [];
        return Lrc.Parse(text.GetString()??"");
    }
    static void EnsureResponse(JsonElement root) {
        if(root.ValueKind!=JsonValueKind.Object)throw new JsonException("网易云响应格式错误");
        if(root.TryGetProperty("code",out var code) && (code.ValueKind!=JsonValueKind.Number || !code.TryGetInt32(out int value) || value!=200))throw new HttpRequestException("网易云服务拒绝请求");
    }
}
