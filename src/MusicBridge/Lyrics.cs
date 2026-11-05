using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;
public record LyricLine(long StartMs, string Text);
public static class Lrc {
    public static List<LyricLine> Parse(string text) {
        var result = new List<LyricLine>();
        long offset = 0;
        var om = Regex.Match(text, @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase);
        if (om.Success) long.TryParse(om.Groups[1].Value, out offset);
        foreach (var line in text.Split('\n')) {
            var stamps = Regex.Matches(line, @"\[(\d{1,3}):([0-5]\d)(?:[.:](\d{1,3}))?\]");
            if (stamps.Count == 0) continue;
            var content = line[(stamps[^1].Index + stamps[^1].Length)..].Trim();
            if(content.Length>2048) content=content[..2048];
            foreach (Match m in stamps) {
                long ms = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)*60000 + long.Parse(m.Groups[2].Value)*1000;
                if (m.Groups[3].Success) ms += int.Parse(m.Groups[3].Value.PadRight(3, '0'));
                result.Add(new(ms + offset, content));
            }
        }
        return result.OrderBy(x => x.StartMs).GroupBy(x => x.StartMs).Select(g => new LyricLine(g.Key, string.Join(" / ", g.Select(l=>l.Text).Distinct()))).ToList();
    }
    public static int Find(IReadOnlyList<LyricLine> lines, long time) {
        int lo=0, hi=lines.Count-1, found=-1;
        while(lo<=hi) { int mid=(lo+hi)/2; if(lines[mid].StartMs<=time) {found=mid;lo=mid+1;} else hi=mid-1; }
        return found;
    }
}
public interface ILyricsProvider { List<LyricLine> Get(string title, string artist, long durationMs); }
// Exact metadata sidecars avoid fuzzy matches to the wrong recording. Only user-owned local files.
public sealed class LocalLyricsProvider(string root) : ILyricsProvider {
    public List<LyricLine> Get(string title, string artist, long durationMs) {
        string[] files;
        try {Directory.CreateDirectory(root);files=Directory.EnumerateFiles(root,"*.json").Take(2000).ToArray();}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) {return [];}
        foreach(var file in files) {
            try {
                if(new FileInfo(file).Length>16384) continue;
                var meta=System.Text.Json.JsonSerializer.Deserialize<Metadata>(File.ReadAllText(file));
                if(meta == null || meta.DurationMs<0 || !string.Equals(meta.Title?.Trim(),title.Trim(),StringComparison.OrdinalIgnoreCase) || !string.Equals(meta.Artist?.Trim(),artist.Trim(),StringComparison.OrdinalIgnoreCase)) continue;
                if(meta.DurationMs > 0 && (durationMs <= 0 || Math.Abs(meta.DurationMs-durationMs)>2000)) continue;
                var lrc=Path.ChangeExtension(file,".lrc");
                if(File.Exists(lrc) && new FileInfo(lrc).Length<=2_000_000) return Lrc.Parse(File.ReadAllText(lrc));
            } catch(Exception e) when(e is System.Text.Json.JsonException or IOException or ArgumentException or UnauthorizedAccessException) { Console.Error.WriteLine("LRC: " + e.Message); }
        }
        return [];
    }
    public sealed class Metadata { public string Title {get;set;}=""; public string Artist {get;set;}=""; public long DurationMs {get;set;} }
}
