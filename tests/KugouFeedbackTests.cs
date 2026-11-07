using DeepSpaceMusic;
static class KugouFeedbackTests {
    public static void Run(Action<bool,string> check) {
        check(KugouClock.WindowArtist("Singer - Song - 酷狗音乐","Song","Singer",out _) && KugouClock.WindowArtist("Song - Singer - 酷狗音乐","Song","Singer",out _),"KuGou title/artist order variants both bind to the same media identity");
        check(KugouClock.WindowArtist("正在播放：Song—Singer—酷狗音乐","Song","Singer",out _) && KugouClock.WindowArtist("Song-Singer - KuGou","Song","Singer",out _),"KuGou branding, playback prefix and separator spacing do not suppress a verified clock");
        check(!KugouClock.WindowArtist("Other - Song - 酷狗音乐","Song","Singer",out _) && !KugouClock.WindowArtist("Singer - Song Live - 酷狗音乐","Song","Singer",out _) && !KugouClock.WindowArtist("酷狗音乐","Song","Singer",out _),"wrong artists, recording versions and generic KuGou windows cannot supply timing");
        check(KugouClock.WindowArtist("Song - Some Artist - 酷狗音乐","Song","",out string artist) && artist=="Some Artist" && !KugouClock.WindowArtist("Song - 酷狗音乐","Song","",out _),"missing SMTC artist is filled only by an unambiguous window title, preserving case");
        check(!KugouClock.WindowArtist("Song - Singer - Song - 酷狗音乐","Song","",out _),"ambiguous title boundaries cannot choose an artist");
        check(KugouClock.Parse("播放进度：０２：０５ ／ ０４：４９",out long pos,out long dur) && pos==125000 && dur==289000,"labelled and full-width KuGou clock remains an actual elapsed/total reading");
        check(KugouClock.Part("01:23",out pos) && pos==83000 && !KugouClock.Part("01:99",out _) && !KugouClock.Parse("00:23 / 00:00",out _,out _),"split clock components validate time fields and reject missing duration");
        check(KugouClock.Same((10000,200000),(11000,200000)) && !KugouClock.Same((10000,200000),(12000,200000)) && !KugouClock.Same((10000,200000),(10000,201000)),"duplicate clocks must agree within one tick and have the same duration");
        var missing=new Snapshot {connected=true,platform="kugou",title="Song",artist="Singer"};
        check(LyricReloadFeedback.Describe(missing).Contains("缺少歌曲时长") && !LyricReloadFeedback.Describe(missing).Contains("已重新匹配"),"reload with zero duration explains missing timeline without claiming lyric success");
        check(LyricReloadFeedback.Describe(new(){connected=true,title="Song",durationMs=200000}).Contains("缺少歌名或歌手"),"reload separates missing artist from lyric-provider failures");
        check(LyricReloadFeedback.Describe(new(){connected=true,title="Song",artist="Singer",durationMs=200000,lyricLineCount=20}).Contains("缺少可靠进度"),"lyrics found without playback timing do not report synchronized success");
        check(LyricReloadFeedback.Describe(new(){connected=true,title="Song",artist="Singer",durationMs=200000,lyricsState="loading_lrclib"}).Contains("已重新发起"),"a pending reload acknowledges the button immediately");
        check(PlaybackPresentation.Details(missing).Contains("普通主界面") && !PlaybackPresentation.Details(missing).Contains("拖动进度一次"),"KuGou missing timing no longer gives the unhelpful generic seek instruction");
    }
}
