using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
namespace DeepSpaceMusic;

// Local playback never queries a lyric service or uploads file names/paths.
public sealed class LocalFolderPlayer : IDisposable {
    public static readonly string[] Extensions={".mp3",".wav",".flac",".m4a",".aac",".wma"};
    readonly object gate=new();readonly string config;
    string[] files=[];int index,revision;bool initialized,disposed,picking,loading,active;
    MediaPlayer? player;MediaSource? source;string title="",artist="",message="请选择音乐文件夹",error="";List<LyricLine> lines=[];
    public LocalFolderPlayer(string root){config=Path.Combine(root,"local-folder.txt");}
    public static string[] Catalog(string folder)=>Directory.EnumerateFiles(Path.GetFullPath(folder),"*",SearchOption.TopDirectoryOnly)
        .Where(f=>Extensions.Contains(Path.GetExtension(f),StringComparer.OrdinalIgnoreCase)).OrderBy(f=>Path.GetFileName(f),StringComparer.OrdinalIgnoreCase).Take(5000).ToArray();
    public static List<LyricLine> Sidecar(string audio) {
        string path=Path.ChangeExtension(audio,".lrc");if(!File.Exists(path))return [];
        if(new FileInfo(path).Length>2_000_000)throw new InvalidDataException("同名 LRC 超过 2 MB");
        return Lrc.Parse(File.ReadAllText(path)).Select(l=>l with {Text=LyricText.ToSimplified(l.Text)}).ToList();
    }
    void EnsureLoaded() {
        if(initialized)return;initialized=true;
        try {if(File.Exists(config)){string folder=File.ReadAllText(config).Trim();_ = SetFolder(folder,false);}}
        catch(Exception e){error="本地文件夹恢复失败："+e.Message;}
    }
    public string PickFolder() {
        lock(gate){if(disposed)throw new ObjectDisposedException(nameof(LocalFolderPlayer));if(picking)return "文件夹选择窗口已打开，请完成选择或取消。";picking=true;}
        var thread=new Thread(()=>{
            try {
                var dialog=new Microsoft.Win32.OpenFolderDialog {Title="选择本地音乐文件夹（仅扫描当前层）",Multiselect=false};
                if(dialog.ShowDialog()==true)_=SetFolder(dialog.FolderName,true);
                else lock(gate)message="已取消选择，保留当前文件夹。";
            }catch(Exception e){lock(gate)error="无法选择文件夹："+e.Message;}
            finally{lock(gate)picking=false;}
        }) {IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        return "请选择本地音乐文件夹；选择完成后返回游戏，使用磁带机播放。";
    }
    public async Task SetFolder(string folder,bool persist=true) {
        int version;
        lock(gate){if(disposed)return;initialized=true;version=++revision;loading=true;}
        try {
            var catalog=await Task.Run(()=>Catalog(folder));
            if(catalog.Length==0)throw new InvalidDataException("文件夹没有支持的音频；请放入 MP3、WAV、FLAC、M4A、AAC 或 WMA。");
            lock(gate){if(disposed || version!=revision)return;
                if(persist){Directory.CreateDirectory(Path.GetDirectoryName(config)!);File.WriteAllText(config,Path.GetFullPath(folder));}
                files=catalog;index=0;message="已载入 "+files.Length+" 首；使用磁带机播放／暂停、上下首。";
            }
            await Load(0,false,version);
        }catch(Exception e){lock(gate)if(!disposed && version==revision){loading=false;error="本地文件夹加载失败："+e.Message;}}
    }
    async Task Load(int selected,bool play,int version) {
        string path;lock(gate){if(disposed || version!=revision)return;path=files[selected];loading=true;}
        try {
            var file=await StorageFile.GetFileFromPathAsync(path);var tags=await file.Properties.GetMusicPropertiesAsync();
            List<LyricLine> lyric;string lyricError="";
            try{lyric=Sidecar(path);}catch(Exception e){lyric=[];lyricError="同名歌词读取失败："+e.Message;}
            lock(gate){if(disposed || version!=revision)return;
                player?.Dispose();source?.Dispose();var media=new MediaPlayer {AutoPlay=false};player=media;
                media.CommandManager.IsEnabled=false;
                media.MediaFailed+=(sender,args)=>{lock(gate)if(ReferenceEquals(player,sender))error="音频无法解码；请使用未加密且 Windows 支持的文件（"+args.Error+"）。";};
                media.MediaEnded+=(sender,args)=>{lock(gate)if(!ReferenceEquals(player,sender) || disposed)return;Advance(1);};
                title=string.IsNullOrWhiteSpace(tags.Title)?Path.GetFileNameWithoutExtension(path):tags.Title;
                artist=string.IsNullOrWhiteSpace(tags.Artist)?"本地文件":tags.Artist;index=selected;lines=lyric;error=lyricError;loading=false;
                source=MediaSource.CreateFromStorageFile(file);media.Source=source;if(play && active)media.Play();
            }
        }catch(Exception e){lock(gate)if(!disposed && version==revision){loading=false;player?.Dispose();player=null;error="音频加载失败："+e.Message;}}
    }
    void Advance(int delta) {
        lock(gate){if(disposed || !active || files.Length==0)return;int selected=(index+delta+files.Length)%files.Length;int version=++revision;player?.Pause();_ = Load(selected,true,version);}
    }
    public (bool ok,string result) Control(string command) {
        lock(gate){active=true;EnsureLoaded();if(player==null || loading)return(false,"请先选择有效音乐文件夹，等待音频加载完成。");
            switch(command){case "play":player.Play();break;case "pause":player.Pause();break;
                case "next":Advance(1);break;case "previous":Advance(-1);break;
                case "stop":player.Pause();player.PlaybackSession.Position=TimeSpan.Zero;break;
                case "restart":player.PlaybackSession.Position=TimeSpan.Zero;break;default:return(false,"不支持的本地播放操作");}
            return(true,"本地播放操作已应用。");}
    }
    public void Pause(){lock(gate){active=false;revision++;loading=false;player?.Pause();}}
    public void Reload(){lock(gate)if(files.Length>0){try{lines=Sidecar(files[index]);error="";}catch(Exception e){error=e.Message;}}}
    public Snapshot Read(bool enabled,int offset) {
        lock(gate){active=enabled;if(enabled){EnsureLoaded();if(player==null && files.Length>0 && !loading && error.Length==0)_=Load(index,false,revision);}
            var s=new Snapshot{platform="local",sourceApp="local",bridgeVersion="0.2.7",connectionEnabled=enabled,detected=files.Length>0,
                connected=enabled && files.Length>0,title=title,artist=artist,offsetMs=offset,error=error,
                timelineDetail=loading?"正在读取本地音频":picking?"正在选择音乐文件夹":message};
            if(!s.connected){s.error=error.Length>0?error:enabled?message:"";return s;}
            var session=player?.PlaybackSession;s.durationMs=Math.Max(0,(long)(session?.NaturalDuration.TotalMilliseconds??0));
            s.positionMs=Math.Clamp((long)(session?.Position.TotalMilliseconds??0),0,s.durationMs);
            s.playback=session?.PlaybackState==MediaPlaybackState.Playing?"playing":loading?"opening":"paused";
            s.trackId="local|"+revision+"|"+index;s.canPlay=player!=null && !loading;s.canPause=s.canPlay;s.canStop=s.canPlay;s.canNext=s.canPlay && files.Length>1;s.canPrevious=s.canNext;
            s.positionConfidence=s.durationMs>0?"local_player":"unavailable";s.lyricLineCount=lines.Count;
            s.lyricsSource=lines.Count>0?"同名本地 LRC":"";s.lyricsState=lines.Count==0?"no_matching_lyrics":s.durationMs>0?"matched":"timeline_unreliable";
            s.lyricsDetail=lines.Count==0?"音频旁放置同名 UTF-8 LRC；本地模式不会联网匹配歌词。":"";
            if(s.durationMs>0 && lines.Count>0){int i=Lrc.Find(lines,s.positionMs+offset);if(i>=0){s.current=lines[i].Text;s.currentStartMs=lines[i].StartMs;}if(i+1<lines.Count)s.next=lines[i+1].Text;}
            return s;}
    }
    public void Dispose(){lock(gate){disposed=true;revision++;player?.Dispose();source?.Dispose();player=null;source=null;}}
}
