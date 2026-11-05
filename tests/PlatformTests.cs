using DeepSpaceMusic;
using System.Text.Json;
static class PlatformTests {
    public static void Run(Action<bool,string> check) {
        var reader=new NetEaseTimeline("unused-test-log");var pids=new HashSet<int>{42};
        string Line(long tick,string payload)=>$"[42:1:1106/003223:{tick}:INFO:app.cpp(1231)] "+payload;
        reader.IngestLine(Line(1000,"\"native资源load完成\",{\"songId\":\"123\"}"),1000,pids);
        check(!reader.StateAt(1200).Anchored,"NetEase resource loading alone cannot invent playback timing");
        reader.IngestLine(Line(1100,"\"native播放state\",1"),1100,pids);
        check(reader.StateAt(2100).Position==1000 && reader.StateAt(2100).Playing,"NetEase timing is anchored to a real playback event");
        reader.IngestLine(Line(2200,"\"setPlayingPosition\",15.372"),2200,pids);
        reader.IngestLine(Line(3747,"\"native播放state\",2"),3747,pids);
        check(reader.StateAt(9000).Position==16919 && !reader.StateAt(9000).Playing,"NetEase seek and pause reconstruct exact event position and freeze");
        reader.IngestLine(Line(4000,"\"setPlayingPosition\",4.125"),4000,pids);
        check(reader.StateAt(9000).Position==4125,"NetEase backward seek while paused replaces the old anchor");
        reader.IngestLine(Line(5000,"\"native播放state\",1"),5000,new HashSet<int>{99});
        check(!reader.StateAt(9000).Playing,"log events from a different process cannot change playback");
        reader.IngestLine(Line(5100,"\"native播放state\",1"),5100,pids);
        reader.IngestLine(Line(6100,"\"native播放state\",3"),6100,pids);
        check(!reader.StateAt(9000).Anchored && !reader.StateAt(9000).Playing,"unknown native state invalidates timing instead of counting through buffering");
        reader.IngestLine(Line(7000,"\"native资源load完成\",{\"songId\":\"456\"}"),7000,pids);
        check(reader.StateAt(9000).Id=="456" && !reader.StateAt(9000).Anchored,"track changes clear the previous song timing");
        check(KugouTimeline.ParseTime("02:05/04:49",out long pos,out long dur) && pos==125000 && dur==289000,"KuGou displayed clock parses into a real timeline");
        check(KugouTimeline.ParseTime("1:02:03 / 2:01:02",out pos,out dur) && pos==3723000 && dur==7262000,"hour-long tracks preserve full clock precision");
        check(!KugouTimeline.ParseTime("04:99/05:00",out _,out _) && !KugouTimeline.ParseTime("05:01/05:00",out _,out _) && !KugouTimeline.ParseTime("00:00/00:00",out _,out _) && !KugouTimeline.ParseTime("123456789",out _,out _),"malformed or missing client clock cannot synthesize lyrics timing");
        using var candidates=JsonDocument.Parse("""{"candidates":[{"song":"Song","singer":"Singer","duration":"bad","id":"1","accesskey":"public"},{"song":"Song Live","singer":"Singer","duration":200000,"id":"2","accesskey":"public"},{"song":"Song","singer":"Singer","duration":200900,"id":"3","accesskey":"public"},{"song":"Song","singer":"Other","duration":200000,"id":"4","accesskey":"public"},{"song":"Song","singer":"Singer","duration":200200,"id":"5","accesskey":"public"},{"song":"Song","singer":"Singer","duration":210000,"id":"6","accesskey":"public"}]}""");
        var selected=KugouLyrics.Candidates(candidates.RootElement,"Song","Singer",200000);
        check(selected.Count==2 && selected[0].Id=="5","KuGou lyrics reject wrong artist, version, duration and malformed fields, then choose closest duration");
        using var track=JsonDocument.Parse("""{"songs":[{"id":123,"name":"Song","duration":200000,"artists":[{"name":"Singer"}]}]}""");
        check(NetEaseTimeline.ParseTrack(track.RootElement,"123")?.Duration==200000 && NetEaseTimeline.ParseTrack(track.RootElement,"456")==null,"NetEase public metadata must belong to the exact logged song ID");
        using var malformed=JsonDocument.Parse("""{"songs":[{"id":123,"name":"Song","duration":"bad","artists":[]}]}""");
        check(NetEaseTimeline.ParseTrack(malformed.RootElement,"123")==null,"malformed public song metadata cannot break the bridge");
    }
}
