using DeepSpaceMusic;

static class FollowupTests {
    public static void Run(Action<bool,string> check) {
        var disabledBridge=new Snapshot {bridge="ready",platform="qq",connectionEnabled=false};
        check(PlaybackPresentation.NeedsReconnect(disabledBridge,"qq",true,true),"enabled game reconnects when a restarted same-platform bridge restores a disabled connection");
        check(!PlaybackPresentation.NeedsReconnect(disabledBridge,"qq",false,true) && !PlaybackPresentation.NeedsReconnect(disabledBridge,"qq",true,false),"same-platform recovery still respects explicit disconnect and disabled automatic recovery");

        var reader=new NetEaseTimeline("unused-followup-log");var live=new HashSet<int>{42,43};
        string Line(int pid,long tick,string payload)=>$"[{pid}:1:1106/003223:{tick}:INFO:app.cpp(1)] "+payload;
        reader.IngestLine(Line(42,1000,"\"native资源load完成\",{\"songId\":\"123\"}"),3000,live);
        reader.IngestLine(Line(42,2000,"\"native播放state\",1"),3000,live);
        reader.IngestLine(Line(42,2500,"\"setPlayingPosition\",12.5"),3000,live);
        reader.IngestLine(Line(42,1500,"\"native资源load完成\",{\"songId\":\"111\"}"),3000,live);
        check(reader.StateAt(3000).Id=="123" && reader.StateAt(3000).Position==13000 && reader.StateAt(3000).Anchored,"out-of-order NetEase load cannot replace a newer song or erase its seek anchor");
        reader.IngestLine(Line(43,1600,"\"native资源load完成\",{\"songId\":\"222\"}"),3000,live);
        check(reader.StateAt(3000).Id=="123","older load from another live NetEase process cannot replace the current song");
        reader.IngestLine(Line(43,3100,"\"native资源load完成\",{\"songId\":\"456\"}"),3200,live);
        check(reader.StateAt(3200).Id=="456" && !reader.StateAt(3200).Anchored,"a newer genuine track load still clears timing and selects the new song");

        check(Lrc.Parse("[00:00.00] \n[00:02.00]\t").Count==0,"timestamped blank lyrics cannot mask another provider's actual lyrics");
        var clear=Lrc.Parse("[00:01]first\n[00:03]\n[00:05]next");
        check(clear.Count==3 && clear[1].Text=="" && Lrc.Find(clear,4000)==1,"timed blank breaks remain in real lyrics instead of extending the previous line");
        using(var json=System.Text.Json.JsonDocument.Parse("""[{"trackName":"Song","artistName":"Singer","duration":200,"syncedLyrics":"[00:00] "},{"trackName":"Song","artistName":"Singer","duration":201,"syncedLyrics":"[00:02]valid alternative"}]"""))
            check(AutomaticLyrics.ParseResponse("Song","Singer",200000,json.RootElement).Single().Text=="valid alternative","a closest-duration blank search result cannot hide a valid timed alternative");
        string folder=Path.Combine(Path.GetTempPath(),"DeepSpaceMusicFollowup-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            foreach(string name in new[]{"a","b"})File.WriteAllText(Path.Combine(folder,name+".json"),"""{"Title":"Song","Artist":"Singer","DurationMs":200000}""");
            var metadata=Directory.GetFiles(folder,"*.json");
            File.WriteAllText(Path.ChangeExtension(metadata[0],".lrc"),"metadata only, no timed lyrics");
            File.WriteAllText(Path.ChangeExtension(metadata[1],".lrc"),"[00:02]valid local line");
            var lines=new LocalLyricsProvider(folder).Get("Song","Singer",200000);
            check(lines.Count==1 && lines[0].StartMs==2000,"an empty matching local sidecar cannot hide another valid matching LRC file");
        } finally {Directory.Delete(folder,true);}
    }
}
