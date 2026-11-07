using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

// Presentation variants only: never select an unrelated window or invent a clock.
public static class KugouClock {
    public static bool WindowArtist(string window,string title,string artist,out string resolvedArtist) {
        resolvedArtist="";
        static string Display(string value)=>Regex.Replace((value??"").Normalize(NormalizationForm.FormKC).Replace('–','-').Replace('—','-').Trim(),@"\s*-\s*"," - ");
        string name=Display(window),song=Display(title);
        if(song.Length==0)return false;
        foreach(string brand in new[]{" - 酷狗音乐"," - 酷狗"," - KUGOU MUSIC"," - KUGOU"})
            if(name.EndsWith(brand,StringComparison.OrdinalIgnoreCase)){name=name[..^brand.Length].Trim();break;}
        foreach(string prefix in new[]{"正在播放：","正在播放:","播放中：","播放中:"})
            if(name.StartsWith(prefix,StringComparison.Ordinal)){name=name[prefix.Length..].Trim();break;}
        var choices=new List<string>();
        for(int index=name.IndexOf(" - ",StringComparison.Ordinal);index>=0;index=name.IndexOf(" - ",index+3,StringComparison.Ordinal)) {
            string left=name[..index].Trim(),right=name[(index+3)..].Trim();
            if(MetadataMatch.Title(left,song))choices.Add(right);
            if(MetadataMatch.Title(right,song))choices.Add(left);
        }
        choices=choices.Where(x=>x.Length>0 && !MetadataMatch.Title(x,song)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(artist.Trim().Length>0)choices=choices.Where(x=>MetadataMatch.Artists(x,Display(artist))).ToList();
        if(choices.Count!=1)return false;
        resolvedArtist=artist.Trim().Length>0?artist:choices[0];return true;
    }
    static readonly Regex clock=new(@"^(?:(?:播放进度|播放时间|进度|时间)\s*[:：]?\s*)?((?:\d{1,2}:)?\d{1,2}:\d{2})\s*/\s*((?:\d{1,2}:)?\d{1,2}:\d{2})$",RegexOptions.Compiled);
    public static bool Part(string text,out long ms) {
        ms=0;var fields=text.Trim().Split(':');
        if(fields.Length is <2 or >3)return false;
        for(int i=0;i<fields.Length;i++) {
            if(!int.TryParse(fields[i],NumberStyles.None,CultureInfo.InvariantCulture,out int n) || n<0 || (i>0 && n>=60))return false;
            ms=ms*60+n;
        }
        ms*=1000;return ms<=86400000;
    }
    public static bool Parse(string text,out long position,out long duration) {
        position=duration=0;
        if(text==null)return false;
        var m=clock.Match(text.Normalize(NormalizationForm.FormKC).Trim());
        return m.Success && Part(m.Groups[1].Value,out position) && Part(m.Groups[2].Value,out duration) && duration>0 && position<=duration;
    }
    public static bool Same((long Position,long Duration) a,(long Position,long Duration) b)=>
        a.Duration==b.Duration && Math.Abs(a.Position-b.Position)<=1000;
}
