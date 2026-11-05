namespace DeepSpaceMusic;
// Remember manual intent separately from the transient availability of a session.
public sealed class ConnectionPolicy {
    public bool Enabled {get;private set;}=true;
    public bool AutoReconnect {get;private set;}=true;
    public bool Requested {get;private set;}
    public void Restore(bool autoReconnect,bool enabled) {AutoReconnect=autoReconnect;Enabled=enabled;Requested=enabled && autoReconnect;}
    public void Configure(bool autoReconnect) {
        if(!AutoReconnect && autoReconnect && Enabled)Requested=true;
        AutoReconnect=autoReconnect;
    }
    public void Request(bool value) {Requested=value;if(value)Enabled=true;}
    public void Disconnect() {Enabled=false;Requested=false;}
    public void Lost() {Requested=Enabled && AutoReconnect;}
}
