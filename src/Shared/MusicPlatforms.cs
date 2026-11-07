using System;
namespace DeepSpaceMusic;
public static class MusicPlatforms {
    public static readonly string[] Ids={"netease","qq","kugou","local"};
    public static readonly string[] Choices={"未选择","netease","qq","kugou","local"};
    public static readonly string[] Labels={"请选择平台","网易云音乐","QQ 音乐","酷狗音乐","本地文件夹音乐"};
    public static bool IsSupported(string id)=>id=="netease" || id=="qq" || id=="kugou" || id=="local";
    public static int Index(string id)=>Math.Max(0,Array.IndexOf(Choices,id));
    public static string Name(string id)=>Labels[Index(id)];
    public static string OpenLabel(string id)=>id=="local"?"选择音乐文件夹":IsSupported(id)?"打开 "+Name(id)+" 选歌":"选择音乐平台";
    public static string SetupHint(string id)=>id=="local"?"选择本地音乐文件夹，使用未加密音频和同名 UTF-8 LRC；无需 SMTC":id=="netease"?"请在网易云设置 → 系统开启 SMTC，并播放一首歌曲":id=="kugou"?"请在酷狗设置 → 常规设置 → 播放开启系统播放控件，并播放一首歌曲":"请启动 QQ 音乐并开启系统媒体传输控件，再播放歌曲";
}
