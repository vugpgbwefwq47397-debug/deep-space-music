using System.Diagnostics;
using System.Runtime.InteropServices;
namespace DeepSpaceMusic;

public static class ClientPanel {
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int show);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    static DateTime lastOpen=DateTime.MinValue;
    public static string Open(string platform) {
        if(platform!="qq")throw new InvalidOperationException("当前选歌入口支持 QQ 音乐");
        if(DateTime.UtcNow-lastOpen<TimeSpan.FromSeconds(2))return "已请求打开 QQ 音乐，请在客户端选歌";
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IntPtr window=IntPtr.Zero;
        foreach(var process in Process.GetProcessesByName("QQMusic"))using(process) {
            try {if(process.MainModule?.FileName is string path) {paths.Add(path);if(process.MainWindowHandle!=IntPtr.Zero)window=process.MainWindowHandle;}}
            catch(System.ComponentModel.Win32Exception) { }
        }
        if(paths.Count>1)throw new InvalidOperationException("检测到不同安装位置的 QQ 音乐，请先关闭多余客户端");
        string? executable=paths.SingleOrDefault();
        executable ??= new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}
            .Select(root=>Path.Combine(root,"Tencent","QQMusic","QQMusic.exe")).FirstOrDefault(File.Exists);
        if(executable==null || !File.Exists(executable))throw new FileNotFoundException("未找到 QQ 音乐，请先手动启动客户端");
        // Explicit user button only. No protocol, song ID, batch command or
        // native module is used; normal no-argument launch opens the client.
        if(window!=IntPtr.Zero) {ShowWindowAsync(window,9);if(SetForegroundWindow(window)) {lastOpen=DateTime.UtcNow;return "已请求打开 QQ 音乐，请在客户端选歌";}}
        using var launched=Process.Start(new ProcessStartInfo(executable) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(executable)!});
        if(launched==null)throw new IOException("未能打开 QQ 音乐，请手动打开客户端");
        lastOpen=DateTime.UtcNow;
        return "已请求打开 QQ 音乐，请在客户端选歌";
    }
}
