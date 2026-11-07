using System.Windows.Automation;
namespace DeepSpaceMusic;

// Read only. UI providers run on one background worker and never block the pipe.
public sealed class KugouTimeline : IDisposable {
    sealed record Query(string Title,string Artist,int Generation);
    sealed record Sample(Query Query,string Artist,long Position,long Duration,long At,long Changed);
    sealed record Notice(Query Query,string Text,long At);
    sealed record Binding(AutomationElement First,AutomationElement? Second=null,bool Value=false) {
        public bool Read(out long position,out long duration) {
            string text=Value && First.TryGetCurrentPattern(ValuePattern.Pattern,out var value)?((ValuePattern)value).Current.Value:First.Current.Name;
            if(Second!=null)text+=" / "+Second.Current.Name;
            return ParseTime(text,out position,out duration);
        }
    }
    Query? requested;
    Sample? sample;
    Notice? notice;
    volatile bool stopped;
    bool started;
    int generation;
    public string Diagnostic {
        get {var q=Volatile.Read(ref requested);var n=Volatile.Read(ref notice);return q!=null && n?.Query==q?n.Text+(Environment.TickCount64-n.At>8000 && n.Text.StartsWith("正在")?"；客户端读取超过 8 秒，暂不滚动歌词。桥接仍可响应，可稍后重试。":""):"正在后台检测酷狗播放时钟";}
    }
    void Notify(Query query,string text)=>Volatile.Write(ref notice,new(query,text,Environment.TickCount64));
    public static bool ParseTime(string text,out long position,out long duration)=>KugouClock.Parse(text,out position,out duration);
    public bool TryApply(Snapshot state) {
        var query=new Query(state.title,state.artist,Volatile.Read(ref generation));
        Volatile.Write(ref requested,query);
        if(!started){started=true;Task.Factory.StartNew(Worker,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
        var value=Volatile.Read(ref sample);long now=Environment.TickCount64;
        if(value==null || value.Query!=query || now-value.At>1500)return false;
        if(state.playback=="playing" && now-value.Changed>2200) {
            Notify(query,"酷狗时钟未随播放更新，暂不滚动歌词。请确认使用普通主界面；无需反复重新匹配歌词。");return false;
        }
        long advance=state.playback=="playing"?Math.Clamp(now-value.Changed,0,1000):0;
        state.positionMs=Math.Min(value.Duration,value.Position+advance);state.durationMs=value.Duration;
        if(state.artist.Trim().Length==0)state.artist=value.Artist;
        state.positionConfidence="kugou_accessibility";return true;
    }
    static List<Binding> Discover(AutomationElement root) {
        // Search shallow containers first, without entering playlist/web content.
        // KuGou's embedded pages can make a whole-window Descendants query take
        // minutes even though the native playback bar has only a few texts.
        var cache=new CacheRequest {TreeScope=TreeScope.Element};
        cache.Add(AutomationElement.NameProperty);cache.Add(AutomationElement.ControlTypeProperty);cache.Add(AutomationElement.BoundingRectangleProperty);
        using var active=cache.Activate();
        var queue=new Queue<(AutomationElement Element,int Depth)>();queue.Enqueue((root,0));
        AutomationElement? playbar=null;long deadline=Environment.TickCount64+8000;int visited=0;
        while(queue.Count>0 && visited<256 && Environment.TickCount64<deadline) {
            var (parent,depth)=queue.Dequeue();var children=parent.FindAll(TreeScope.Children,Condition.TrueCondition);
            var containers=new List<AutomationElement>();
            foreach(AutomationElement child in children) {
                visited++;var type=child.Cached.ControlType;string name=child.Cached.Name;
                if(type==ControlType.Slider && (name=="进度" || name=="播放进度" || name.Equals("progress",StringComparison.OrdinalIgnoreCase))) {playbar=parent;break;}
                if(depth<7 && (type==ControlType.Custom || type==ControlType.Pane || type==ControlType.Group || type==ControlType.Tab || type==ControlType.ToolBar))containers.Add(child);
            }
            if(playbar!=null)break;
            foreach(var container in containers.OrderByDescending(x=>x.Cached.BoundingRectangle.Top))queue.Enqueue((container,depth+1));
        }
        if(playbar==null)return new();
        var condition=new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Slider),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ProgressBar));
        var elements=playbar.FindAll(TreeScope.Descendants,condition);var found=new List<Binding>();var groups=new HashSet<string>();
        for(int i=0;i<Math.Min(elements.Count,512);i++) {
            var e=elements[i];
            string name=e.Cached.Name;
            if(ParseTime(name,out _,out _)){found.Add(new(e));continue;}
            if((name.Length==0 || e.Cached.ControlType!=ControlType.Text) && e.TryGetCurrentPattern(ValuePattern.Pattern,out var value) && ParseTime(((ValuePattern)value).Current.Value,out _,out _))found.Add(new(e,Value:true));
            // Some skins split the two times. Accept only two sibling clocks
            // grouped with a progress control, never unrelated playlist durations.
            if(!KugouClock.Part(name,out _))continue;
            var parent=TreeWalker.ControlViewWalker.GetParent(e);if(parent==null)continue;
            if(!groups.Add(string.Join(".",parent.GetRuntimeId())))continue;
            var children=parent.FindAll(TreeScope.Children,Condition.TrueCondition);
            if(children.Count>16)continue;
            bool progress=false;var times=new List<AutomationElement>();
            foreach(AutomationElement child in children) {
                var type=child.Current.ControlType;
                progress|=type==ControlType.Slider || type==ControlType.ProgressBar;
                if(type==ControlType.Text && KugouClock.Part(child.Current.Name,out _))times.Add(child);
            }
            if(progress && times.Count==2) {
                times=times.OrderBy(x=>x.Current.BoundingRectangle.Left).ToList();
                var binding=new Binding(times[0],times[1]);if(binding.Read(out _,out _))found.Add(binding);
            }
        }
        return found;
    }
    void Worker() {
        Query? previousQuery=null;List<Binding>? bindings=null;IntPtr window=IntPtr.Zero;long nextDiscovery=0;string artist="";
        while(!stopped) {
            var query=Volatile.Read(ref requested);
            if(query!=null && query.Title.Trim().Length>0)try {
                if(query!=previousQuery){previousQuery=query;bindings=null;nextDiscovery=0;Volatile.Write(ref sample,null);}
                if(bindings==null && Environment.TickCount64>=nextDiscovery) {
                    nextDiscovery=Environment.TickCount64+3000;
                    Notify(query,"正在识别酷狗播放窗口");
                    var candidates=new List<(IntPtr Handle,string Artist)>();
                    foreach(var p in KugouWindows.Find())
                        if(KugouClock.WindowArtist(p.Title,query.Title,query.Artist,out string singer))candidates.Add((p.Handle,singer));
                    if(candidates.Count==1) {
                        window=candidates[0].Handle;artist=candidates[0].Artist;
                        Notify(query,"正在读取酷狗无障碍播放时钟");
                        bindings=Discover(AutomationElement.FromHandle(window));
                        if(bindings.Count==0)bindings=null;
                        Notify(query,bindings==null?"酷狗窗口已匹配，但未读到播放时钟。请使用普通主界面；当前皮肤或迷你模式可能不支持。":"已读取酷狗播放时钟（秒级）");
                    } else Notify(query,candidates.Count>1?"多个酷狗窗口匹配当前歌曲，暂不使用有歧义的进度。":"未找到与当前歌名、歌手一致的酷狗窗口。请打开普通主界面播放歌曲；桥接不会主动调出客户端。");
                }
                if(bindings!=null) {
                    if(!KugouClock.WindowArtist(KugouWindows.Title(window),query.Title,query.Artist,out string currentArtist) || !MetadataMatch.Artists(artist,currentArtist))throw new InvalidOperationException();
                    var readings=new List<(long Position,long Duration)>();
                    foreach(var binding in bindings)if(binding.Read(out long pos,out long dur))readings.Add((pos,dur));
                    if(readings.Count==0)throw new InvalidOperationException();
                    var reading=readings[0];
                    if(readings.Any(x=>!KugouClock.Same(reading,x))) {
                        Volatile.Write(ref sample,null);bindings=null;
                        Notify(query,"酷狗返回多个不同的播放时钟，暂不采用，避免歌词错位。");
                    } else {
                        long now=Environment.TickCount64;var previous=Volatile.Read(ref sample);
                        long changed=previous!=null && previous.Query==query && previous.Position==reading.Position?previous.Changed:now;
                        Volatile.Write(ref sample,new(query,artist,reading.Position,reading.Duration,now,changed));
                    }
                }
            } catch(Exception e) when(e is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or ArgumentException or UnauthorizedAccessException) {
                bindings=null;Volatile.Write(ref sample,null);
                Notify(query,"酷狗播放时钟暂不可访问（"+e.GetType().Name+"）。请使用普通主界面，并让游戏、桥接和客户端以相同权限运行。");
            }
            Thread.Sleep(250);
        }
    }
    public void Reset(){Interlocked.Increment(ref generation);Volatile.Write(ref requested,null);Volatile.Write(ref sample,null);Volatile.Write(ref notice,null);}
    public void Dispose(){stopped=true;Reset();}
}
