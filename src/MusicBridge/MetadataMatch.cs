using System.Text;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

// Normalize presentation differences, never discard version/live/remix qualifiers
// or accept only a subset of a collaboration's artists.
public static class MetadataMatch {
    public static string Text(string value) => Regex.Replace((value??"").Normalize(NormalizationForm.FormKC)
        .Replace('’','\'').Replace('‘','\'').Replace('–','-').Replace('—','-').Trim(),@"\s+"," ").ToUpperInvariant();
    public static bool Title(string a,string b)=>Text(a)==Text(b);
    public static bool Artists(string a,string b) {
        if(Text(a)==Text(b))return true;
        static string[] Parts(string value)=>Regex.Split(Text(value),@"\s*(?:/|、|;|；|&|,|，|\bAND\b)\s*")
            .Where(x=>x.Length>0).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var left=Parts(a);var right=Parts(b);
        return left.Length>0 && left.SequenceEqual(right);
    }
    public static bool Recording(string title,string artist,long duration,string candidateTitle,string candidateArtist,double seconds)=>
        duration>0 && double.IsFinite(seconds) && Title(title,candidateTitle) && Artists(artist,candidateArtist) && Math.Abs(duration-seconds*1000)<=2000;
}
