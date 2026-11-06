using System;
using System.IO;
namespace DeepSpaceMusic;
public static class BridgeFailures {
    public static string Describe(Exception error) {
        if(error is UnauthorizedAccessException)return "本地管道访问被拒绝。请用同一 Windows 账号、相同运行权限启动游戏和 MusicBridge，建议两者都不要以管理员运行。";
        if(error is TimeoutException)return "未连接到 MusicBridge。工坊订阅不包含桥接：请先解压 Windows 包，运行独立的 MusicBridge/MusicBridge.exe 并保持开启。SMTC 开启不能替代本地桥接。";
        if(error is PlatformNotSupportedException)return "当前运行环境不支持本地命名管道；需要 Windows x64。";
        if(error is IOException)return "MusicBridge 本地通信中断，请查看桥接窗口报错并重新启动桥接；确认游戏和桥接使用同一 Windows 账号及运行权限。";
        return "本地桥接连接失败（"+error.GetType().Name+"）："+error.Message;
    }
}
