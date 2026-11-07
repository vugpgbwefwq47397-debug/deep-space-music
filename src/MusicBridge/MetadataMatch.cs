using System.Text;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

// Normalize presentation differences, never discard version/live/remix qualifiers
// or accept only a subset of a collaboration's artists.
public static class MetadataMatch {
    public static string Text(string value) => Regex.Replace((value??"").Normalize(NormalizationForm.FormKC)
        .Replace('’','\'').Replace('‘','\'').Replace('–','-').Replace('—','-').Trim(),@"\s+"," ").ToUpperInvariant();
    // Explicit is a content-rating suffix, often absent from another catalog's
    // title. Preserve Clean, Radio Edit, Live, remix and all other qualifiers.
    public static string SearchTitle(string value)=>Regex.Replace(Text(value),@"\s*[\(\[]EXPLICIT[\)\]]\s*$","").Trim();
    public static bool Title(string a,string b)=>SearchTitle(a)==SearchTitle(b);
    static string[] ArtistParts(string value) {
        string text=LyricText.ToSimplified(Text(value));
        text=Regex.Replace(text,@"\s+(?:FEAT(?:URING)?|FT)(?:\.\s*|\s+)"," / ");
        return Regex.Split(text,@"\s*(?:/|、|;|；|&|,|，|\bAND\b)\s*").Where(x=>x.Length>0).ToArray();
    }
    public static string SearchArtist(string value)=>ArtistParts(value).FirstOrDefault()??"";
    public static bool Artists(string a,string b) {
        if(Text(a)==Text(b))return true;
        var left=ArtistParts(a).OrderBy(x=>x,StringComparer.Ordinal).ToArray();var right=ArtistParts(b).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        return left.Length>0 && left.SequenceEqual(right);
    }
    public static bool Recording(string title,string artist,long duration,string candidateTitle,string candidateArtist,double seconds)=>
        duration>0 && double.IsFinite(seconds) && Title(title,candidateTitle) && Artists(artist,candidateArtist) && Math.Abs(duration-seconds*1000)<=2000;
}
