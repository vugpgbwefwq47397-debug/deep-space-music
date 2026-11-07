using System.Runtime.InteropServices;
using System.Text;
namespace DeepSpaceMusic;

// Discover player windows without the system-wide .NET process/performance scan.
// Only query-limited process handles are opened; no process memory or input access.
public static class KugouWindows {
    public sealed record Window(IntPtr Handle,int ProcessId,string Title);
    delegate bool WindowCallback(IntPtr handle,IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr state);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static string Title(IntPtr handle){var text=new StringBuilder(2048);GetWindowText(handle,text,text.Capacity);return text.ToString();}
    public static List<Window> Find() {
        var found=new List<Window>();var identities=new Dictionary<uint,bool>();
        EnumWindows((handle,_)=>{
            GetWindowThreadProcessId(handle,out uint pid);
            if(!identities.TryGetValue(pid,out bool match)) {
                var process=OpenProcess(0x1000,false,pid);match=false;
                if(process!=IntPtr.Zero)try {
                    var path=new StringBuilder(2048);uint length=(uint)path.Capacity;
                    match=QueryFullProcessImageName(process,0,path,ref length) && Path.GetFileName(path.ToString()).Equals("KuGou.exe",StringComparison.OrdinalIgnoreCase);
                } finally {CloseHandle(process);}
                identities[pid]=match;
            }
            if(match){string title=Title(handle);if(title.Length>0)found.Add(new(handle,(int)pid,title));}
            return true;
        },IntPtr.Zero);
        return found;
    }
}
