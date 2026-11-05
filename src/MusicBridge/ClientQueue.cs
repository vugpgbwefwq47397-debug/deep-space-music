using System.IO;
namespace DeepSpaceMusic;

// The QQ database is read by an isolated, bounded x86 helper. No window API.
public sealed class ClientQueue {
    readonly object sync=new();
    readonly SemaphoreSlim gate=new(1,1);
    readonly Func<Task<QueueTrack[]>> read;
    readonly Func<string> stamp;
    QueueTrack[]? tracks;
    int version,page,generation;
    string state="unavailable",error="双击磁带或点击刷新读取当前队列",loadedStamp="";
    DateTime checkedAt;
    public int Total {get {lock(sync)return tracks?.Length??0;}}
    public ClientQueue(Func<Task<QueueTrack[]>>? read=null,Func<string>? stamp=null) {this.read=read??QqQueueReader.Read;this.stamp=stamp??QqQueueReader.Stamp;}
    public void Clear() {lock(sync) {generation++;page=0;tracks=null;loadedStamp="";state="unavailable";error="已断开，请连接音乐客户端后读取队列";version++;}}
    public void Apply(Snapshot snapshot) {lock(sync) {
        snapshot.queueState=state;snapshot.queueError=error;
        snapshot.queuePage=page;snapshot.queueTotal=tracks?.Length??0;snapshot.queueVersion=version;
        snapshot.queue=state=="ready"?tracks!.Skip(page*10).Take(10).ToArray():[];
        snapshot.queueText=state=="ready"?string.Join("\n",snapshot.queue.Select((song,i)=>$"{page*10+i+1}. {Display(song.title)} — {Display(song.artist)}")):"";
    }}
    static string Display(string text)=>text.Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
    public QueueTrack Select(int index,int expectedVersion) {lock(sync) {
        if(state!="ready" || tracks==null || version!=expectedVersion || stamp()!=loadedStamp)throw new InvalidOperationException("队列已变化，请刷新后再选择歌曲");
        if(index<0 || index>=tracks.Length)throw new InvalidDataException("歌曲位置超出队列范围");
        if(!ulong.TryParse(tracks[index].catalogId,out var id) || id==0 || id>uint.MaxValue)throw new InvalidOperationException("此曲目没有可用的 QQ 在线歌曲编号");
        return tracks[index];
    }}
    public static void AdaptForClient(Snapshot snapshot,int clientVersion) {
        // Existing in-game Mod versions cannot display the nested track array
        // in this Unity runtime. Their status-label path accepts a text page.
        // Modern clients use queueText with a normal ready state and no error.
        if(clientVersion<2 && snapshot.queueState=="ready") {snapshot.queueState="ready-text";snapshot.queueError=snapshot.queueText.Length>0?snapshot.queueText:"当前播放队列为空";}
    }
    public void Request(string platform,int requestedPage,bool refresh) {
        if(requestedPage<0 || requestedPage>9999) throw new InvalidDataException("页码超出范围");
        if(platform is not ("qq" or "netease")) throw new InvalidDataException("请先选择音乐平台");
        string currentStamp="";try {currentStamp=stamp();}catch(IOException) {}catch(UnauthorizedAccessException) {}
        int job;
        lock(sync) {
            page=requestedPage;version++;
            if(platform!="qq") {generation++;tracks=null;state="unavailable";error="网易云音乐当前队列尚未适配后台读取";return;}
            if(state=="loading")return;
            if(!refresh && tracks!=null && currentStamp==loadedStamp && DateTime.UtcNow-checkedAt<TimeSpan.FromSeconds(30)) {state="ready";page=Math.Min(page,Math.Max(0,(tracks.Length-1)/10));error="";return;}
            job=++generation;tracks=null;state="loading";error="";
        }
        _=Task.Run(async()=>{
            await gate.WaitAsync();
            try {
                lock(sync) {if(job!=generation)return;}
                string before=stamp();var result=await read();string after=stamp();
                // File changed during the read: do not tag this snapshot as fresh.
                // The next page request re-reads instead of reusing a stale cache.
                lock(sync) {if(job!=generation)return;tracks=result;loadedStamp=before==after?after:"";checkedAt=DateTime.UtcNow;page=Math.Min(page,Math.Max(0,(result.Length-1)/10));state="ready";error="";version++;}
            }catch(Exception e) {lock(sync) {if(job==generation) {tracks=null;state="unavailable";error=e.Message;version++;}}}
            finally {gate.Release();}
        });
    }
}
