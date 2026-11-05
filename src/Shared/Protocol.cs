using System;
namespace DeepSpaceMusic;
[Serializable] public class Request {
    public int protocolVersion = 1;
    public int clientVersion;
    public string action = "status", platform = "", command = "";
    public bool autoReconnect = true;
    public bool connectionEnabled = true;
    public int offsetMs;
    public int page;
    public bool refresh;
    public int queueIndex,queueVersion;
}
[Serializable] public class Snapshot {
    public int protocolVersion = 1;
    public bool ok = true, connected, detected, autoReconnect;
    public bool connectionEnabled = true;
    public string bridge = "ready", platform = "", playback = "unknown", error = "", result = "";
    public string title = "", artist = "", trackId = "", sourceApp = "";
    public long positionMs, durationMs;
    public int offsetMs;
    public string positionConfidence = "unavailable", lyricsState = "unavailable", current = "", next = "";
    public long currentStartMs = -1;
    public bool canPlay, canPause, canNext, canPrevious, canStop;
    public string queueState="unavailable", queueError="", queueText="";
    public int queuePage, queueTotal, queueVersion;
    public QueueTrack[] queue = Array.Empty<QueueTrack>();
}
[Serializable] public class QueueTrack { public string title="", artist="",catalogId="";public long durationMs; }
