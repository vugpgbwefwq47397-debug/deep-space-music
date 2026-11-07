using DeepSpaceMusic;
static class ReportedSongTests {
 public static void Run(Action<bool,string> check) {
  check(MetadataMatch.Artists("Maroon 5、Christina Aguilera","Maroon 5 feat. Christina Aguilera"),"featured credits preserve both artists across catalog separators");
  check(MetadataMatch.Artists("陈冠希/MC仁/厨房仔/应采儿","陳冠希, 廚房仔 & MC仁 feat. 應采兒"),"reported Everywhere collaboration accepts simplified/traditional credit presentation");
  check(!MetadataMatch.Artists("Rosa Walton/Hallie Coggins","Rosa Walton") && !MetadataMatch.Artists("A/B","A feat. C"),"credit normalization still rejects missing and substituted collaborators");
  check(MetadataMatch.Recording("STAY (Explicit)","The Kid LAROI/Justin Bieber",141810,"STAY","The Kid LAROI & Justin Bieber",141.81),"content-rating-only title suffix does not hide a matching timed recording");
  check(!MetadataMatch.Title("STAY (Explicit)","STAY (Clean)") && !MetadataMatch.Title("Wasted (Explicit)","Wasted (Radio Edit)"),"explicit cannot match a candidate labelled clean or radio edit");
  check(!MetadataMatch.Title("Moves Like Jagger (Soul Seekerz Radio Edit)","Moves Like Jagger") && !MetadataMatch.Title("Song (Live)","Song"),"remix and live recording qualifiers remain mandatory");
  check(!MetadataMatch.Recording("STAY (Explicit)","The Kid LAROI/Justin Bieber",141810,"STAY","The Kid LAROI/Justin Bieber",150),"content rating normalization never widens duration tolerance");
  check(KugouClock.WindowArtist("The Kid LAROI、Justin Bieber - STAY - 酷狗音乐","STAY (Explicit)","The Kid LAROI/Justin Bieber",out _),"KuGou window identity accepts a missing content-rating suffix with all artists verified");
  check(MetadataMatch.SearchArtist("Juice WRLD/Lil Uzi Vert")=="JUICE WRLD" && MetadataMatch.SearchTitle("Wasted (Explicit)")=="WASTED","search uses primary artist while recording validation still checks every collaborator");
  check(PlaybackPresentation.Details(new(){connected=true,platform="kugou",bridgeVersion="0.2.2",title="Song",artist="Artist"}).StartsWith("旧桥接缺少酷狗时钟修复"),"reported 0.2.2 KuGou bridge gets an explicit upgrade instruction instead of only a seek suggestion");
 }
}
