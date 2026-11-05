namespace DeepSpaceMusic;

// Restoring a known queue only. Building a command never starts a process or
// changes the client. Ordinary song selection must preserve the existing list.
public sealed class QueueBatchCommand {
    public QueueTrack[] Tracks {get;}
    public string Argument {get;}
    QueueBatchCommand(QueueTrack[] tracks,string argument) {Tracks=tracks;Argument=argument;}
    public static QueueBatchCommand Create(IReadOnlyList<QueueTrack> queue,string executable) {
        if(queue.Count==0 || queue.Count>10000)throw new InvalidDataException("队列范围无效");
        var tracks=new QueueTrack[queue.Count];var ids=new string[queue.Count];
        for(int i=0;i<queue.Count;i++) {
            var track=queue[i];
            if(track==null || string.IsNullOrWhiteSpace(track.title) || track.artist==null ||
                !uint.TryParse(track.catalogId,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out uint id) || id==0)
                throw new InvalidDataException("队列含无法核对的在线歌曲编号，未发送选曲命令");
            ids[i]=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tracks[i]=new QueueTrack {title=track.title,artist=track.artist,catalogId=ids[i],durationMs=track.durationMs};
        }
        // cmd_count counts command groups, rather than songs. A batch uses one
        // group with equally sized ID and server-song-type lists.
        string argument="cmd_count==1&&id_0=="+string.Join(',',ids)+"&&songtype_0=="+string.Join(',',Enumerable.Repeat("0",ids.Length));
        // Windows' UTF-16 command line limit includes the executable and quotes.
        if(executable.Length+argument.Length+"\"\" /playbysongid \"\"".Length+1>32767)
            throw new InvalidDataException("当前队列超出客户端批量命令长度限制，未发送选曲命令");
        return new QueueBatchCommand(tracks,argument);
    }
}
