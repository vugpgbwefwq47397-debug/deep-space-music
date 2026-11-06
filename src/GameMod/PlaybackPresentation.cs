using System;
namespace DeepSpaceMusic;

public static class PlaybackPresentation {
    public static bool NeedsReconnect(Snapshot s,string selected,bool enabled,bool autoReconnect)=>
        enabled && autoReconnect && MusicPlatforms.IsSupported(selected) && (s.bridge=="offline" || s.platform!=selected || !s.connectionEnabled);
    public static float Reveal(bool playing,bool reducedMotion,float elapsed,int durationMs) =>
        !playing || reducedMotion || durationMs<=0?1:(float)Math.Max(0,Math.Min(1,elapsed/(durationMs/1000f)));
    public static string LyricsStatus(Snapshot s) {
        if(s.positionConfidence=="unavailable" && s.lyricsState=="matched")return "等待播放进度";
        switch(s.lyricsState) {
            case "matched":return "歌词已同步";
            case "no_matching_lyrics":return "暂无匹配歌词，显示歌名";
            case "lyrics_lookup_failed":return "歌词获取失败，可在工具中重试";
            case "timeline_unreliable":return "歌词已匹配，等待可靠播放进度";
            case "waiting_for_metadata":return "等待完整歌曲信息";
            case "loading_lrclib":return "正在匹配歌词";
            default:return "等待歌词";
        }
    }
    public static string Status(Snapshot s) {
        if(s.bridge=="offline")return "桥接未连接\n请启动 MusicBridge";
        if(!s.connected)return "未连接音乐客户端\n请打开连接与工具";
        string playback=s.playback=="playing"?"正在播放":s.playback=="paused"?"已暂停":s.playback=="stopped"?"已停止":"等待播放";
        return s.title+" — "+s.artist+"\n"+playback+" · "+LyricsStatus(s);
    }
    public static string Details(Snapshot s) {
        if(s.error.Length>0)return s.error;
        if(s.bridge=="offline")return "请下载并独立运行 MusicBridge；只订阅工坊、开启 SMTC 还不能连接本地桥接。";
        if(!s.connected)return "本地桥接已连接；请先在音乐客户端播放歌曲，再选择平台并连接。";
        string info="桥接 "+(s.bridgeVersion.Length>0?s.bridgeVersion:"旧版")+" · "+LyricsStatus(s);
        info+="\n进度："+(s.positionConfidence=="unavailable"?"未提供":s.positionConfidence)+" · 时长 "+s.durationMs/1000+" 秒";
        if(s.lyricsSource.Length>0)info+=" · 来源 "+s.lyricsSource+" / "+s.lyricLineCount+" 行";
        if(s.positionConfidence=="unavailable")info+="\n"+(s.timelineDetail.Length>0?s.timelineDetail:"请在客户端播放或拖动进度一次；尚无可靠进度时只显示歌名。");
        if(s.lyricsDetail.Length>0)info+="\n"+s.lyricsDetail;
        else if(s.lyricsState=="no_matching_lyrics")info+="\n各来源暂无该录音的匹配时间轴，可自备 LRC，或点击重新匹配歌词。";
        return info;
    }
    public static Snapshot Normalize(Snapshot s) {
        if(s==null || s.protocolVersion!=1)throw new InvalidOperationException("桥接返回了无效状态或协议版本");
        s.bridge=s.bridge??"offline";s.platform=s.platform??"";s.playback=s.playback??"unknown";
        s.error=s.error??"";s.result=s.result??"";s.title=s.title??"";s.artist=s.artist??"";
        s.trackId=s.trackId??"";s.sourceApp=s.sourceApp??"";s.current=s.current??"";s.next=s.next??"";
        s.positionConfidence=s.positionConfidence??"unavailable";s.lyricsState=s.lyricsState??"unavailable";
        s.bridgeVersion=s.bridgeVersion??"";s.lyricsSource=s.lyricsSource??"";s.lyricsDetail=s.lyricsDetail??"";s.timelineDetail=s.timelineDetail??"";
        return s;
    }
}
