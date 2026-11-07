using Windows.Media.Control;
using System.IO;
namespace DeepSpaceMusic;
public sealed class Sessions : IDisposable {
    GlobalSystemMediaTransportControlsSessionManager? manager;
    GlobalSystemMediaTransportControlsSession? target;
    readonly ILyricsProvider lyrics;
    public readonly LocalFolderPlayer Local;
    readonly SemaphoreSlim readGate=new(1,1);
    int revision;
    string key="";
    List<LyricLine> lines=[];
    List<LyricLine>? rawLines;
    readonly NetEaseTimeline netease=new();
    readonly KugouTimeline kugou=new();
    readonly ConnectionPolicy connection=new();
    public string Platform="";
    public bool AutoReconnect {get=>connection.AutoReconnect;set=>connection.Configure(value);}
    public bool WantConnection {get=>connection.Requested;set=>connection.Request(value);}
    public bool ConnectionEnabled=>connection.Enabled;
    public void RestoreConnection(bool autoReconnect,bool enabled)=>connection.Restore(autoReconnect,enabled);
    public int Offset;
    public Sessions(ILyricsProvider provider,string? root=null) { lyrics=provider;Local=new(root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeepSpaceMusic")); }
    public static bool Matches(string platform, string id) {
        // Exact unpackaged executable identities, plus known packaged identity prefix.
        var name=Path.GetFileNameWithoutExtension(id);
        return platform switch {
            "netease" => name.Equals("cloudmusic",StringComparison.OrdinalIgnoreCase) || id.StartsWith("NetEase.CloudMusic_",StringComparison.OrdinalIgnoreCase),
            "qq" => name.Equals("QQMusic",StringComparison.OrdinalIgnoreCase) || id.StartsWith("Tencent.QQMusic_",StringComparison.OrdinalIgnoreCase),
            "kugou" => name.Equals("kugou",StringComparison.OrdinalIgnoreCase),
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
        if(Platform=="local") {var local=Local.Read(WantConnection,Offset);local.connectionEnabled=ConnectionEnabled;local.autoReconnect=AutoReconnect;return local;}
        var s=new Snapshot {platform=Platform, autoReconnect=AutoReconnect,connectionEnabled=ConnectionEnabled,offsetMs=Offset,bridgeVersion="0.2.6"};
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
        token.ThrowIfCancellationRequested();
        if(version!=revision) throw new OperationCanceledException("音乐连接已变化");
        var candidates=manager.GetSessions().Where(x=>Matches(Platform,x.SourceAppUserModelId)).ToArray();
        s.detected=candidates.Length>0;
        if(target != null && !candidates.Contains(target)) {target=null;key="";lines.Clear();kugou.Reset();connection.Lost();}
        if(target == null && WantConnection) {
            if(candidates.Length==1) target=candidates[0];
            else s.error=candidates.Length>1?"检测到多个同平台会话，请关闭多余客户端":"未检测到媒体会话。"+MusicPlatforms.SetupHint(Platform);
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
        if(Platform=="netease") {netease.TryApply(s);s.timelineDetail=netease.Diagnostic;}
        if(s.positionConfidence=="unavailable" && Platform=="kugou") {kugou.TryApply(s);s.timelineDetail=kugou.Diagnostic;}
        s.trackId=s.sourceApp+"|"+s.title+"|"+s.artist+"|"+s.durationMs;
        string lyricKey=s.trackId;
        if(lyrics is AutomaticLyrics platformLyrics) {
            platformLyrics.Platform=Platform;platformLyrics.NetEaseSongId=Platform=="netease"?netease.VerifiedSongId:"";
            lyricKey+="|"+platformLyrics.NetEaseSongId;
        }
        var fetched=lyrics.Get(s.title,s.artist,s.durationMs);
        if(lyricKey!=key || !ReferenceEquals(fetched,rawLines)) {
            key=lyricKey;
            rawLines=fetched;
            lines=fetched.Select(line=>line with {Text=LyricText.ToSimplified(line.Text)}).ToList();
        }
        s.lyricLineCount=lines.Count;
        if(lyrics is AutomaticLyrics lookup) {s.lyricsSource=lookup.Source;s.lyricsDetail=lookup.Diagnostic;}
        if(lines.Count>0 && s.positionConfidence!="unavailable") {
            s.lyricsState="matched"; var i=Lrc.Find(lines,s.positionMs+Offset);
            if(i>=0) {s.current=lines[i].Text;s.currentStartMs=lines[i].StartMs;}
            if(i+1<lines.Count) s.next=lines[i+1].Text;
        } else s.lyricsState=lines.Count>0?"timeline_unreliable":lyrics is AutomaticLyrics automatic?automatic.Status:"unavailable";
        return s;
    }
    public void Disconnect(bool manual=false) {revision++;Local.Pause();target=null;if(manual)connection.Disconnect();else WantConnection=false;key="";lines.Clear();kugou.Reset();}
    public void Dispose() {Local.Dispose();kugou.Dispose();readGate.Dispose();}
    public void ReloadLyrics() { if(Platform=="local")Local.Reload();key="";lines.Clear();rawLines=null;if(lyrics is AutomaticLyrics automatic)automatic.Reset(); }
    public async Task<(bool ok,string result)> Control(string command,CancellationToken token=default) {
        if(Platform=="local")return ConnectionEnabled && WantConnection?Local.Control(command):(false,"请先连接本地文件夹音乐");
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
