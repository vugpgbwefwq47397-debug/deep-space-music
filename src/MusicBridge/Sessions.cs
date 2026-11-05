using Windows.Media.Control;
using System.IO;
namespace DeepSpaceMusic;
public sealed class Sessions {
    GlobalSystemMediaTransportControlsSessionManager? manager;
    GlobalSystemMediaTransportControlsSession? target;
    readonly ILyricsProvider lyrics;
    readonly SemaphoreSlim readGate=new(1,1);
    int revision;
    string key="";
    List<LyricLine> lines=[];
    public string Platform="";
    public bool AutoReconnect=true, WantConnection;
    public int Offset;
    public Sessions(ILyricsProvider provider) { lyrics=provider; }
    public static bool Matches(string platform, string id) {
        // Exact unpackaged executable identities, plus known packaged identity prefix.
        var name=Path.GetFileNameWithoutExtension(id);
        return platform switch {
            "netease" => name.Equals("cloudmusic",StringComparison.OrdinalIgnoreCase) || id.StartsWith("NetEase.CloudMusic_",StringComparison.OrdinalIgnoreCase),
            "qq" => name.Equals("QQMusic",StringComparison.OrdinalIgnoreCase) || id.StartsWith("Tencent.QQMusic_",StringComparison.OrdinalIgnoreCase),
            _=>false
        };
    }
    public async Task<Snapshot> Read() {
        return await Read(CancellationToken.None);
    }
    public async Task<Snapshot> Read(CancellationToken token) {
        await readGate.WaitAsync(token);
        try {return await ReadCore(token);} finally {readGate.Release();}
    }
    async Task<Snapshot> ReadCore(CancellationToken token) {
        int version=revision;
        var s=new Snapshot {platform=Platform, autoReconnect=AutoReconnect,offsetMs=Offset};
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
        token.ThrowIfCancellationRequested();
        if(version!=revision) throw new OperationCanceledException("音乐连接已变化");
        var candidates=manager.GetSessions().Where(x=>Matches(Platform,x.SourceAppUserModelId)).ToArray();
        s.detected=candidates.Length>0;
        if(target != null && !candidates.Contains(target)) {target=null;key="";lines.Clear(); if(!AutoReconnect) WantConnection=false;}
        if(target == null && WantConnection) {
            if(candidates.Length==1) target=candidates[0];
            else s.error=candidates.Length>1?"检测到多个同平台会话，请关闭多余客户端":"未检测到所选平台媒体会话，请启动客户端并播放歌曲";
        }
        s.connected=target != null;
        if(target == null) { s.error = s.error.Length>0?s.error:Platform.Length==0?"请首次手动选择音乐平台":"未连接音乐客户端"; return s; }
        s.sourceApp=target.SourceAppUserModelId;
        var session=target;
        var props=await session.TryGetMediaPropertiesAsync().AsTask(token);
        token.ThrowIfCancellationRequested();
        if(version!=revision) throw new OperationCanceledException("音乐连接已变化");
        s.title=props.Title ?? ""; s.artist=props.Artist ?? "";
        var playback=session.GetPlaybackInfo();
        s.playback=playback.PlaybackStatus.ToString().ToLowerInvariant();
        var c=playback.Controls;
        s.canPlay=c.IsPlayEnabled; s.canPause=c.IsPauseEnabled; s.canNext=c.IsNextEnabled; s.canPrevious=c.IsPreviousEnabled; s.canStop=c.IsStopEnabled;
        var t=session.GetTimelineProperties();
        s.durationMs=Math.Max(0,(long)(t.EndTime-t.StartTime).TotalMilliseconds);
        var age=DateTimeOffset.UtcNow-t.LastUpdatedTime;
        // Never synthesize timing from a missing/default timeline. Re-sample on every request for seeks.
        if(s.durationMs>0 && t.Position>=t.StartTime && t.Position<=t.EndTime && age.TotalSeconds>=-2 && age.TotalHours<12) {
            double advance=s.playback=="playing" ? Math.Max(0,age.TotalMilliseconds)*(playback.PlaybackRate ?? 1d):0;
            s.positionMs=Math.Clamp((long)((t.Position-t.StartTime).TotalMilliseconds+advance),0,s.durationMs);
            s.positionConfidence=s.playback=="playing"?"estimated":"reported";
        }
        s.trackId=s.sourceApp+"|"+s.title+"|"+s.artist+"|"+s.durationMs;
        if(s.trackId!=key || lines.Count==0) {
            key=s.trackId;
            lines=lyrics.Get(s.title,s.artist,s.durationMs).Select(line=>line with {Text=LyricText.ToSimplified(line.Text)}).ToList();
        }
        if(lines.Count>0 && s.positionConfidence!="unavailable") {
            s.lyricsState="matched"; var i=Lrc.Find(lines,s.positionMs+Offset);
            if(i>=0) {s.current=lines[i].Text;s.currentStartMs=lines[i].StartMs;}
            if(i+1<lines.Count) s.next=lines[i+1].Text;
        } else s.lyricsState=lines.Count>0?"timeline_unreliable":lyrics is AutomaticLyrics automatic?automatic.Status:"unavailable";
        return s;
    }
    public void Disconnect() {revision++;target=null; WantConnection=false; key="";lines.Clear(); }
    public void ReloadLyrics() { key="";lines.Clear();if(lyrics is AutomaticLyrics automatic)automatic.Reset(); }
    public async Task<(bool ok,string result)> Control(string command,CancellationToken token=default) {
        var before=await Read(token); var session=target;
        if(session==null || !before.connected) return(false,before.error);
        bool accepted=command switch {
            "play" when before.canPlay => await session.TryPlayAsync().AsTask(token),
            "pause" when before.canPause => await session.TryPauseAsync().AsTask(token),
            "next" when before.canNext => await session.TrySkipNextAsync().AsTask(token),
            "previous" when before.canPrevious => await session.TrySkipPreviousAsync().AsTask(token),
            "restart" when cSeek(before) => await session.TryChangePlaybackPositionAsync(0).AsTask(token),
            "stop" when before.canStop => await session.TryStopAsync().AsTask(token),
            _=>false
        };
        return(accepted, accepted?"客户端已接受指令；播放状态以实时回读为准":"播放器不支持此操作或拒绝了指令");
    }
    static bool cSeek(Snapshot state)=>state.connected && state.durationMs>0;
}
