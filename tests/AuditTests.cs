using DeepSpaceMusic;
using System.Text.Json;

static class AuditTests {
    sealed class Local : ILyricsProvider {
        public int Reads;
        public List<LyricLine> Lines=[];
        public List<LyricLine> Get(string title,string artist,long durationMs) {Reads++;return Lines;}
    }
    public static async Task Run(Action<bool,string> check) {
        check(MusicPlatforms.Ids.All(MusicPlatforms.IsSupported) && !MusicPlatforms.IsSupported("other"),"all three platforms share the same supported registry");
        check(Sessions.Matches("netease","cloudmusic.exe") && Sessions.Matches("kugou","kugou") && Sessions.Matches("kugou","KuGou.exe"),"verified NetEase and KuGou media identities are recognized");
        check(!Sessions.Matches("kugou","my-kugou") && !Sessions.Matches("netease","kugou") && !Sessions.Matches("qq","kugou"),"KuGou matching rejects substring and cross-platform identities");
        check(MusicPlatforms.Name("netease")=="网易云音乐" && MusicPlatforms.Name("kugou")=="酷狗音乐" && MusicPlatforms.OpenLabel("kugou").Contains("酷狗"),"picker and settings follow the selected platform");
        check(MusicPlatforms.Index("obsolete")==0 && MusicPlatforms.OpenLabel("未选择")=="选择音乐平台","unknown platform leads to selection instead of silently opening QQ");
        string install=Path.Combine(Path.GetTempPath(),"music-platform-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(install,"20.1.61.28014"));
        try {
            string launcher=Path.Combine(install,"KuGou.exe"),child=Path.Combine(install,"20.1.61.28014","KuGou.exe");
            check(ClientPanel.LaunchPath("kugou",child)==child,"missing KuGou launcher does not invent an executable");
            File.WriteAllText(launcher,"");
            check(ClientPanel.LaunchPath("kugou",child)==launcher && ClientPanel.LaunchPath("kugou",launcher)==launcher,"KuGou launcher and versioned children resolve to one installation");
            bool wrongClient=false;try {ClientPanel.LaunchPath("netease",launcher);}catch(InvalidDataException){wrongClient=true;}
            check(wrongClient,"a music client cannot be launched under a different platform");
        } finally {Directory.Delete(install,true);}
        var now=new DateTime(2026,11,5,0,0,0,DateTimeKind.Utc);
        var local=new Local();int fetches=0;
        var pending=new TaskCompletionSource<List<LyricLine>>();
        var lyrics=new AutomaticLyrics(local,(_,_,_)=>{fetches++;return pending.Task;},()=>now);
        for(int i=0;i<200;i++)lyrics.Get("Song","Singer",200000);
        check(local.Reads==1 && fetches==1 && lyrics.Status=="loading_lrclib","rapid polling coalesces local scans and online lookup");
        pending.SetResult([new(1000,"online")]);
        check(lyrics.Get("Song","Singer",200000)[0].Text=="online","pending lookup becomes available without blocking IPC");
        local.Lines=[new(1234,"local override")];now=now.AddSeconds(3);
        check(lyrics.Get("Song","Singer",200000)[0].StartMs==1234 && lyrics.Status=="local","new local lyrics override online cache after bounded refresh");
        lyrics.Reset();local.Lines=[];var old=new TaskCompletionSource<List<LyricLine>>();var fresh=new TaskCompletionSource<List<LyricLine>>();int rounds=0;
        var reset=new AutomaticLyrics(local,(_,_,_)=>++rounds==1?old.Task:fresh.Task,()=>now);
        reset.Get("Song","Singer",200000);reset.Reset();reset.Get("Song","Singer",200000);
        old.SetResult([new(1000,"stale")]);
        check(reset.Get("Song","Singer",200000).Count==0,"reload discards late lyrics from the old lookup");
        fresh.SetResult([new(1000,"fresh")]);
        check(reset.Get("Song","Singer",200000)[0].Text=="fresh","reload accepts only the new lookup");
        int failures=0;
        var retry=new AutomaticLyrics(local,(_,_,_)=>++failures==1?Task.FromException<List<LyricLine>>(new IOException("test network failure")):Task.FromResult(new List<LyricLine>{new(1000,"recovered")}),()=>now);
        retry.Get("Song","Singer",200000);
        check(retry.Status=="lyrics_lookup_failed","failed lookup has a truthful error state");
        for(int i=0;i<100;i++)retry.Get("Song","Singer",200000);
        check(failures==1,"failed lookup does not hammer the service on every poll");
        now=now.AddSeconds(21);
        check(retry.Get("Song","Singer",200000)[0].Text=="recovered" && failures==2,"failed background lookup recovers after retry delay");
        using(var json=JsonDocument.Parse("""
          [{"syncedLyrics":"[00:01]bad missing metadata"},null,{"trackName":"Song","artistName":"Singer","duration":"200","syncedLyrics":"bad type"},
           {"trackName":"Song","artistName":"Singer","duration":200,"syncedLyrics":"not timed lyrics"},
           {"trackName":"Song","artistName":"Singer","duration":201,"syncedLyrics":"[00:01.2]valid timed text"}]
          """)) {
            var found=AutomaticLyrics.ParseResponse("Song","Singer",200000,json.RootElement);
            check(found.Count==1 && found[0].StartMs==1200 && found[0].Text=="valid timed text","malformed search entries and untimed candidates cannot hide a valid match");
        }
        check(PlaybackPresentation.Reveal(false,false,.025f,150)==1,"pause during fade leaves the current lyric fully readable");
        check(Math.Abs(PlaybackPresentation.Reveal(true,false,.075f,150)-.5f)<.001 && PlaybackPresentation.Reveal(true,false,1,150)==1,"playing lyric reveal respects configured duration and completion");
        check(PlaybackPresentation.Reveal(true,true,0,150)==1 && PlaybackPresentation.Reveal(true,false,0,0)==1,"reduced motion and zero duration reveal immediately");
        check(PlaybackPresentation.LyricsStatus(new(){lyricsState="no_matching_lyrics"}).Contains("暂无") && PlaybackPresentation.LyricsStatus(new(){lyricsState="lyrics_lookup_failed"}).Contains("失败"),"no match and lookup failure are not shown as endless loading");
        check(PlaybackPresentation.Status(new(){bridge="offline"}).Contains("桥接") && PlaybackPresentation.Status(new(){connected=true,playback="stopped"}).Contains("已停止"),"offline bridge and stopped playback have distinct status labels");
        var safe=PlaybackPresentation.Normalize(new(){current=null,next=null,title=null,error=null});
        check(safe.current=="" && safe.next=="" && safe.title=="" && safe.error=="","nullable transport fields cannot crash lyric rendering");
        bool rejected=false;try {PlaybackPresentation.Normalize(new(){protocolVersion=999});}catch(InvalidOperationException){rejected=true;}
        check(rejected,"unsupported reply protocol is rejected before rendering");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool cancelledRead=false;
        try {await new Sessions(local).Read(cancelled.Token);}catch(OperationCanceledException){cancelledRead=true;}
        check(cancelledRead,"cancelled media read exits before accessing the player");
        bool exact=true;
        for(int i=0;i<1000;i++)exact&=LyricText.ToSimplified("為什麼天這麼安靜 🎵")=="为什么天这么安静 🎵";
        check(exact,"repeated native conversion returns exact text without trailing buffer garbage");
        string invalidRoot=Path.GetTempFileName();
        try {check(new LocalLyricsProvider(invalidRoot).Get("Song","Singer",200000).Count==0,"unavailable local lyric folder does not break media status or online fallback");}
        finally {File.Delete(invalidRoot);}
    }
}
