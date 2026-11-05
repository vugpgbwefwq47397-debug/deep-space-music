namespace DeepSpaceMusic;
public static class TrackDispatch {
    public static void EnsurePreservesQueue(int count) {if(count!=1)throw new InvalidOperationException("当前指定播放入口会重建 QQ 队列，已暂停多曲队列切歌，等待保留队列的方式验证通过");}
    public static void EnsureCurrent(QueueTrack song,int queueCount,Snapshot actual) {
        EnsurePreservesQueue(queueCount);
        if(!uint.TryParse(song.catalogId,out var id) || id==0)throw new InvalidDataException("歌曲编号无效");
        // SMTC can restart the existing song without launching QQ or rebuilding
        // its queue. Arbitrary-item dispatch remains unavailable until verified.
        if(!actual.connected || actual.platform!="qq" || song.title.Length==0 ||
            actual.title!=song.title || actual.artist!=song.artist ||
            song.durationMs<=0 || actual.durationMs<=0 || Math.Abs((double)actual.durationMs-song.durationMs)>2000)
            throw new InvalidOperationException("尚未验证保留队列的指定选曲方式；当前只能重新播放已核对的单曲");
    }
}
