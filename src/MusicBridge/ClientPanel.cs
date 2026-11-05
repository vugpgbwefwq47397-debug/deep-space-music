using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace DeepSpaceMusic;

public static class ClientPanel {
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int show);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    static readonly Dictionary<string,DateTime> lastOpen=new();
    static string ProcessName(string platform)=>platform switch {"qq"=>"QQMusic","netease"=>"cloudmusic","kugou"=>"KuGou",_=>throw new InvalidOperationException("请先选择音乐平台")};
    // KuGou runs both a launcher and versioned children in one installation.
    public static string LaunchPath(string platform,string executable) {
        if(!Path.GetFileName(executable).Equals(ProcessName(platform)+".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("音乐客户端程序名称不匹配");
        var directory=Path.GetDirectoryName(executable)!;
        if(platform=="kugou" && Version.TryParse(Path.GetFileName(directory),out _)) {
            string launcher=Path.Combine(Path.GetDirectoryName(directory)!,"KuGou.exe");
            if(File.Exists(launcher))return launcher;
        }
        return executable;
    }
    static IEnumerable<string> InstalledPaths(string platform) {
        string exe=ProcessName(platform)+".exe";
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
            using var registry=RegistryKey.OpenBaseKey(hive,view);
            using(var app=registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+exe))
                if(app?.GetValue(null) is string appPath)yield return appPath.Trim('"');
            using var uninstall=registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if(uninstall==null)continue;
            foreach(string key in uninstall.GetSubKeyNames()) {
                using var entry=uninstall.OpenSubKey(key);
                string display=entry?.GetValue("DisplayName") as string ?? "";
                bool match=platform switch {"netease"=>display.StartsWith("网易云音乐",StringComparison.Ordinal),"kugou"=>display.StartsWith("酷狗音乐",StringComparison.Ordinal),"qq"=>display.StartsWith("QQ音乐",StringComparison.OrdinalIgnoreCase) || display.StartsWith("QQ 音乐",StringComparison.OrdinalIgnoreCase),_=>false};
                if(!match)continue;
                if(entry?.GetValue("InstallLocation") is string location && location.Length>0)yield return Path.Combine(location,exe);
                if(entry?.GetValue("DisplayIcon") is string icon) {
                    string path=icon.StartsWith('"')?icon.Split('"').ElementAtOrDefault(1)??"":icon.Split(',')[0];
                    if(Path.GetFileName(path).Equals(exe,StringComparison.OrdinalIgnoreCase))yield return path;
                }
            }
        }
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}) {
            yield return Path.Combine(root,platform=="qq"?@"Tencent\QQMusic":platform=="netease"?@"NetEase\CloudMusic":"KGMusic",exe);
            if(platform=="netease")yield return Path.Combine(root,"CloudMusic",exe);
        }
    }
    public static string Open(string platform) {
        string processName=ProcessName(platform),name=MusicPlatforms.Name(platform);
        string result="已请求打开 "+name+"，请在客户端选歌";
        if(lastOpen.TryGetValue(platform,out var last) && DateTime.UtcNow-last<TimeSpan.FromSeconds(2))return result;
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IntPtr window=IntPtr.Zero;
        foreach(var process in Process.GetProcessesByName(processName))using(process) {
            try {if(process.MainModule?.FileName is string path) {paths.Add(LaunchPath(platform,path));if(process.MainWindowHandle!=IntPtr.Zero)window=process.MainWindowHandle;}}
            catch(System.ComponentModel.Win32Exception) { }
            catch(InvalidOperationException) { }
        }
        if(paths.Count==0)foreach(string path in InstalledPaths(platform).Where(File.Exists))paths.Add(LaunchPath(platform,path));
        if(paths.Count>1)throw new InvalidOperationException("检测到不同安装位置的 "+name+"，请先手动打开需要使用的客户端");
        string? executable=paths.SingleOrDefault();
        if(executable==null || !File.Exists(executable))throw new FileNotFoundException("未找到 "+name+"，请先手动启动客户端");
        // Called only by the explicit picker button, never by media polling.
        if(window!=IntPtr.Zero) {ShowWindowAsync(window,9);if(SetForegroundWindow(window)) {lastOpen[platform]=DateTime.UtcNow;return result;}}
        using var launched=Process.Start(new ProcessStartInfo(executable) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(executable)!});
        if(launched==null)throw new IOException("未能打开 "+name+"，请手动打开客户端");
        lastOpen[platform]=DateTime.UtcNow;return result;
    }
}
