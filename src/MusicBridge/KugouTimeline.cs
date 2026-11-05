using System.Diagnostics;
using System.Globalization;
using System.Windows.Automation;
using System.Text.RegularExpressions;
namespace DeepSpaceMusic;

// Read only. No Invoke, click, scroll, ShowWindow or focus operation is used.
// Run provider calls on one background thread so an unresponsive client cannot
// hold the status pipe or spawn a growing collection of blocked tasks.
public sealed class KugouTimeline : IDisposable {
    sealed record Query(string Title,string Artist,int Generation);
    sealed record Sample(Query Query,long Position,long Duration,long At,long Changed);
    Query? requested;
    Sample? sample;
    volatile bool stopped;
    bool started;
    AutomationElement? timer;
    int owner;
    int generation;
    string expectedWindow="";
    long nextDiscovery;
    static readonly Regex times=new(@"^(\d{1,2}:)?\d{1,2}:\d{2}\s*/\s*(\d{1,2}:)?\d{1,2}:\d{2}$",RegexOptions.Compiled);
    public static bool ParseTime(string text,out long position,out long duration) {
        position=duration=0;if(text==null || !times.IsMatch(text.Trim()))return false;
        var parts=text.Trim().Split('/');
        static bool Parse(string value,out long result) {
            result=0;var fields=value.Trim().Split(':');
            for(int i=0;i<fields.Length;i++) {if(!int.TryParse(fields[i],NumberStyles.None,CultureInfo.InvariantCulture,out int n) || (i>0 && n>=60))return false;result=result*60+n;}
            result*=1000;return true;
        }
        return Parse(parts[0],out position) && Parse(parts[1],out duration) && duration>0 && duration<=86400000 && position<=duration;
    }
    public bool TryApply(Snapshot state) {
        var query=new Query(state.title,state.artist,generation);
        Volatile.Write(ref requested,query);
        if(!started) {started=true;Task.Factory.StartNew(Worker,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
        var value=Volatile.Read(ref sample);long now=Environment.TickCount64;
        if(value==null || value.Query!=query || now-value.At>1500 || (state.playback=="playing" && now-value.Changed>2200))return false;
        long advance=state.playback=="playing"?Math.Clamp(now-value.Changed,0,1000):0;
        state.positionMs=Math.Min(value.Duration,value.Position+advance);state.durationMs=value.Duration;state.positionConfidence="kugou_accessibility";return true;
    }
    void Worker() {
        while(!stopped) {
            var query=Volatile.Read(ref requested);
            if(query!=null && query.Title.Length>0 && query.Artist.Length>0) {
                try {
                    string expected=query.Artist+" - "+query.Title+" - 酷狗音乐";
                    if(expected!=expectedWindow){timer=null;expectedWindow=expected;nextDiscovery=0;Volatile.Write(ref sample,null);}
                    if(timer==null && Environment.TickCount64>=nextDiscovery) {
                        nextDiscovery=Environment.TickCount64+3000;
                        var candidates=new List<(int Id,IntPtr Handle)>();
                        foreach(var p in Process.GetProcessesByName("KuGou"))using(p)if(p.MainWindowHandle!=IntPtr.Zero && p.MainWindowTitle.Trim()==expected)candidates.Add((p.Id,p.MainWindowHandle));
                        if(candidates.Count==1) {
                            owner=candidates[0].Id;
                            var root=AutomationElement.FromHandle(candidates[0].Handle);
                            var labels=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text));
                            var matches=new List<AutomationElement>();
                            foreach(AutomationElement label in labels)if(ParseTime(label.Current.Name,out _,out _))matches.Add(label);
                            if(matches.Count==1)timer=matches[0];
                        }
                    }
                    if(timer!=null) {
                        using var p=Process.GetProcessById(owner);
                        if(p.MainWindowTitle.Trim()!=expected)throw new InvalidOperationException();
                        if(ParseTime(timer.Current.Name,out long position,out long duration)) {
                            long now=Environment.TickCount64;var previous=Volatile.Read(ref sample);
                            long changed=previous!=null && previous.Query==query && previous.Position==position?previous.Changed:now;
                            Volatile.Write(ref sample,new(query,position,duration,now,changed));
                        } else throw new InvalidOperationException();
                    }
                } catch(Exception e) when(e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or ArgumentException) {timer=null;Volatile.Write(ref sample,null);}
            }
            Thread.Sleep(250);
        }
    }
    public void Reset() {generation++;Volatile.Write(ref requested,null);Volatile.Write(ref sample,null);}
    public void Dispose() {stopped=true;Reset();}
}
