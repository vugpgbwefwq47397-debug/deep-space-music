using DeepSpaceMusic;
using System.Text.Json;
static class FeedbackTests {
    sealed class Empty : ILyricsProvider {public List<LyricLine> Get(string title,string artist,long ms)=>[];}
    public static async Task Run(Action<bool,string> check) {
        check(MetadataMatch.Recording("Ｓｏｎｇ （Live）","Singer A / Singer B",200000,"Song (Live)","Singer B、Singer A",200.2),"presentation-only Unicode and multi-artist separators cannot suppress a matching recording");
        check(!MetadataMatch.Recording("Song (Live)","Singer A / Singer B",200000,"Song","Singer A / Singer B",200),"normalization retains live/version qualifiers");
        check(!MetadataMatch.Artists("Singer A / Singer B","Singer A") && !MetadataMatch.Artists("Singer A / Singer B","Singer A / Other"),"a partial or different collaboration cannot match");
        check(!MetadataMatch.Recording("Song","Singer",200000,"Song","Singer",210),"format normalization never relaxes recording duration");
        using(var doc=JsonDocument.Parse("""{"code":200,"lrc":{"lyric":"[00:01.00]第一句\n[00:12.50]第二句"}}"""))
            check(NetEaseLyrics.Parse(doc.RootElement).Count==2 && NetEaseLyrics.Parse(doc.RootElement)[1].StartMs==12500,"NetEase song-ID lyrics retain real line timestamps");
        using(var doc=JsonDocument.Parse("""{"code":200,"nolyric":true}"""))check(NetEaseLyrics.Parse(doc.RootElement).Count==0,"instrumental/missing NetEase lyrics do not invent synchronized lines");
        bool denied=false;using(var doc=JsonDocument.Parse("""{"code":"403"}"""))try {NetEaseLyrics.Parse(doc.RootElement);}catch(HttpRequestException){denied=true;}
        check(denied,"malformed service denial remains a retriable provider failure instead of crashing the fallback chain");
        bool badKugou=false;using(var doc=JsonDocument.Parse("[]"))try {KugouLyrics.ParseDownload(doc.RootElement);}catch(JsonException){badKugou=true;}
        check(badKugou,"non-object Kugou download response remains an isolated provider failure");
        bool badEncoding=false;using(var doc=JsonDocument.Parse("""{"content":"not base64!"}"""))try {KugouLyrics.ParseDownload(doc.RootElement);}catch(JsonException){badEncoding=true;}
        check(badEncoding,"invalid Kugou lyric encoding remains an isolated provider failure");
        using(var doc=JsonDocument.Parse("""{"code":200,"lrc":{"lyric":"plain text without timestamps"}}"""))check(NetEaseLyrics.Parse(doc.RootElement).Count==0,"untimed lyrics cannot claim synchronized playback");
        check((await NetEaseLyrics.Fetch("123&other=456")).Count==0,"unverified song-ID syntax is rejected before networking");
        using(var doc=JsonDocument.Parse("""{"result":{"songs":[{"id":1,"name":"Song","duration":200000,"artists":[{"name":"Singer"}]},{"id":2,"name":"Song (Live)","duration":200000,"artists":[{"name":"Singer"}]},{"id":3,"name":"Song","duration":200000,"artists":[{"name":"Other"}]},{"id":4,"name":"Song","duration":210000,"artists":[{"name":"Singer"}]},{"id":"bad&id=1","name":"Song","duration":200000,"artists":[{"name":"Singer"}]}]}}"""))
            check(NetEaseLyrics.Candidates(doc.RootElement,"Song","Singer",200000).SequenceEqual(new[]{"1"}),"cross-platform NetEase fallback validates song title, every artist, duration and numeric ID");
        var slow=new TaskCompletionSource<AutomaticLyrics.Lookup>();
        var fast=AutomaticLyrics.FirstMatch([slow.Task,Task.FromResult(new AutomaticLyrics.Lookup([new(1000,"fast")],"酷狗"))]);
        check(fast.IsCompletedSuccessfully && (await fast).Source=="酷狗","a successful fast provider returns while another provider is still pending");
        slow.SetResult(new([],"LRCLIB","LRCLIB：请求超时"));
        var recovered=await AutomaticLyrics.FirstMatch([Task.FromResult(new AutomaticLyrics.Lookup([],"LRCLIB","LRCLIB：请求超时")),Task.FromResult(new AutomaticLyrics.Lookup([new(2000,"fallback")],"网易云"))]);
        check(recovered.Lines.Count==1 && recovered.Detail.Length==0,"one unavailable service cannot hide another service's valid timed lyrics");
        var missing=await AutomaticLyrics.FirstMatch([Task.FromResult(new AutomaticLyrics.Lookup([],"LRCLIB")),Task.FromResult(new AutomaticLyrics.Lookup([],"酷狗"))]);
        check(missing.Lines.Count==0 && missing.Detail.Length==0,"all successful no-match responses remain distinct from network failure");
        var failed=await AutomaticLyrics.FirstMatch([Task.FromResult(new AutomaticLyrics.Lookup([],"LRCLIB","LRCLIB：请求超时")),Task.FromResult(new AutomaticLyrics.Lookup([],"酷狗"))]);
        check(failed.Detail.Contains("超时"),"partial service failure is visible instead of falsely claiming exhaustive no-match");
        var a=new TaskCompletionSource<List<LyricLine>>();var b=new TaskCompletionSource<List<LyricLine>>();
        var lyrics=new AutomaticLyrics(new Empty(),(title,_,_)=>title=="A"?a.Task:b.Task);
        lyrics.Get("A","Singer",200000);lyrics.Get("B","Singer",200000);a.SetResult([new(1000,"A only")]);
        check(lyrics.Get("B","Singer",200000).Count==0,"late lyrics from the previous track cannot populate the current track");
        b.SetResult([new(2000,"B only")]);
        check(lyrics.Get("B","Singer",200000).Single().Text=="B only" && lyrics.Get("A","Singer",200000).Single().Text=="A only","each song has independent cached lyrics and switching back remains correct");
        check(MenuHotkey.Matches("F9","F9",false,false,false) && MenuHotkey.Matches("F7","F7",false,false,false),"F9 default and selected low-number function key work without F10");
        check(MenuHotkey.Matches("F7","F10",false,false,false) && MenuHotkey.Matches("F7","F9",true,false,false),"legacy F10 and Ctrl+F9 remain available after rebinding");
        check(!MenuHotkey.Matches("Ctrl+F8","F8",false,false,false) && MenuHotkey.Matches("Ctrl+F8","F8",true,false,false) && !MenuHotkey.Matches("F8","F8",false,true,false),"shortcut modifiers are respected and Alt combinations do not toggle the panel");
        check(MenuHotkey.Normalize("Unknown")=="F9","obsolete shortcut setting recovers to a key available on compact keyboards");
        check(PlaybackPresentation.NeedsReconnect(new(){bridge="ready",platform="kugou"},"qq",true,true),"restarted bridge restoring another platform is reconciled to the game's selected platform");
        check(!PlaybackPresentation.NeedsReconnect(new(){bridge="offline",platform="kugou"},"qq",false,true) && !PlaybackPresentation.NeedsReconnect(new(){bridge="ready",platform="kugou"},"qq",true,false),"platform reconciliation never overrides manual disconnect or disabled auto-reconnect");
        check(BridgeFailures.Describe(new TimeoutException()).Contains("工坊") && BridgeFailures.Describe(new TimeoutException()).Contains("SMTC"),"missing local bridge explains that workshop subscription and SMTC are insufficient");
        check(BridgeFailures.Describe(new UnauthorizedAccessException()).Contains("相同运行权限"),"pipe access denial gives the user actionable Windows account/privilege guidance");
        check(PlaybackPresentation.Details(new(){connected=true,positionConfidence="unavailable",lyricsState="timeline_unreliable",timelineDetail="等待事件"}).Contains("等待事件"),"missing playback timeline shows its actual diagnostic separately from lyric lookup");
        check(PlaybackPresentation.Details(new(){connected=true,lyricsState="lyrics_lookup_failed",lyricsDetail="LRCLIB：请求超时"}).Contains("超时"),"online timeout is visible in the tools page");
    }
}
