using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;
public static class KugouLyrics {
    public record Candidate(string Id,string AccessKey,long Difference);
    public static List<Candidate> Candidates(JsonElement root,string title,string artist,long duration) {
        var result=new List<Candidate>();
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("candidates",out var rows) || rows.ValueKind!=JsonValueKind.Array)return result;
        foreach(var row in rows.EnumerateArray()) {
            if(row.ValueKind!=JsonValueKind.Object || !row.TryGetProperty("song",out var song) || song.ValueKind!=JsonValueKind.String || !row.TryGetProperty("singer",out var singer) || singer.ValueKind!=JsonValueKind.String || !row.TryGetProperty("duration",out var time) || time.ValueKind!=JsonValueKind.Number || !time.TryGetInt64(out long ms))continue;
            if(!AutomaticLyrics.Matches(title,artist,duration,song.GetString()??"",singer.GetString()??"",ms/1000d))continue;
            if(!row.TryGetProperty("id",out var id) || !row.TryGetProperty("accesskey",out var key) || key.ValueKind!=JsonValueKind.String)continue;
            string identifier=id.ToString(),access=key.GetString()??"";
            if(!Regex.IsMatch(identifier,@"^\d{1,24}$") || !Regex.IsMatch(access,@"^[A-Za-z0-9]{1,128}$"))continue;
            result.Add(new(identifier,access,Math.Abs(ms-duration)));
        }
        return result.OrderBy(x=>x.Difference).ToList();
    }
    static async Task<JsonDocument> Read(string uri,CancellationToken token) {
        return await LyricsHttp.Read(uri,token);
    }
    public static async Task<List<LyricLine>> Fetch(string title,string artist,long duration) {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var search=await Read("https://lyrics.kugou.com/search?ver=1&man=yes&client=pc&keyword="+Uri.EscapeDataString(MetadataMatch.SearchTitle(title)+" "+MetadataMatch.SearchArtist(artist))+"&duration="+duration,timeout.Token);
        foreach(var candidate in Candidates(search.RootElement,title,artist,duration).Take(3)) {
            // The key identifies a public lyric document; no account token or cookie.
            using var download=await Read("https://lyrics.kugou.com/download?ver=1&client=pc&id="+candidate.Id+"&accesskey="+candidate.AccessKey+"&fmt=lrc&charset=utf8",timeout.Token);
            var lines=ParseDownload(download.RootElement);if(lines.Count>0)return lines;
        }
        return [];
    }
    public static List<LyricLine> ParseDownload(JsonElement root) {
        if(root.ValueKind!=JsonValueKind.Object)throw new JsonException("酷狗歌词响应格式错误");
        if(!root.TryGetProperty("content",out var content) || content.ValueKind!=JsonValueKind.String)return [];
        try {return Lrc.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(content.GetString()??"")));}
        catch(FormatException) {throw new JsonException("酷狗歌词编码错误");}
    }
}
