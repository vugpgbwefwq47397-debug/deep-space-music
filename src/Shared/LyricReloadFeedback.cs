using System;
namespace DeepSpaceMusic;
public static class LyricReloadFeedback {
    public static string Describe(Snapshot state) {
        if(state.platform=="local")return !state.connected?"请先选择本地音乐文件夹并连接。":state.lyricLineCount>0?"已重新读取同名 LRC："+state.lyricLineCount+" 行。":"未读到有效同名 LRC；请将 UTF-8 时间歌词放在音频旁，再点重新匹配。";
        if(!state.connected)return "尚未连接音乐客户端，请先选择平台并连接。";
        if(string.IsNullOrWhiteSpace(state.title) || string.IsNullOrWhiteSpace(state.artist))return "已重新检测，但客户端仍缺少歌名或歌手，暂时无法准确匹配歌词。";
        if(state.durationMs<=0)return "已重新检测：仍缺少歌曲时长和进度，详见下方提示。";
        if(state.lyricLineCount>0 && state.positionConfidence=="unavailable")return "歌词已获取，但缺少可靠进度，暂不能同步。请查看下方进度提示。";
        if(state.lyricLineCount>0)return "歌词已重新匹配："+state.lyricsSource+" / "+state.lyricLineCount+" 行。";
        if(state.lyricsState=="lyrics_lookup_failed")return "已重新匹配，但歌词请求失败。"+state.lyricsDetail;
        if(state.lyricsState=="no_matching_lyrics")return "已重新匹配，各来源暂未找到这首录音的时间歌词，可使用本地 LRC。";
        return "已重新发起歌词匹配，请稍候；结果会自动更新。";
    }
}
