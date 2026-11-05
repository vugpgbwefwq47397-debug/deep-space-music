using System;
namespace DeepSpaceMusic;

public static class PlaybackPresentation {
    public static float Reveal(bool playing,bool reducedMotion,float elapsed,int durationMs) =>
        !playing || reducedMotion || durationMs<=0?1:(float)Math.Max(0,Math.Min(1,elapsed/(durationMs/1000f)));
    public static string LyricsStatus(Snapshot s) {
        if(s.positionConfidence=="unavailable" && s.lyricsState=="matched")return "等待播放进度";
        switch(s.lyricsState) {
            case "matched":return "歌词已同步";
            case "no_matching_lyrics":return "暂无匹配歌词，显示歌名";
            case "lyrics_lookup_failed":return "歌词获取失败，可在工具中重试";
            case "timeline_unreliable":return "等待可靠播放进度";
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
    public static Snapshot Normalize(Snapshot s) {
        if(s==null || s.protocolVersion!=1)throw new InvalidOperationException("桥接返回了无效状态或协议版本");
        s.bridge=s.bridge??"offline";s.platform=s.platform??"";s.playback=s.playback??"unknown";
        s.error=s.error??"";s.result=s.result??"";s.title=s.title??"";s.artist=s.artist??"";
        s.trackId=s.trackId??"";s.sourceApp=s.sourceApp??"";s.current=s.current??"";s.next=s.next??"";
        s.positionConfidence=s.positionConfidence??"unavailable";s.lyricsState=s.lyricsState??"unavailable";
        return s;
    }
}
