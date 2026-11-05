namespace DeepSpaceMusic;
// A late completion may release only its own request, never a newer operation.
public sealed class PendingOperation {
    readonly double cooldown;
    int sequence,active;
    double readyAt;
    public PendingOperation(double cooldown=0) {this.cooldown=cooldown;}
    public bool TryBegin(double now,out int token) {
        token=0;if(active!=0 || now<readyAt)return false;
        token=active=++sequence;return true;
    }
    public void Complete(int token,double now) {if(active!=token || token==0)return;active=0;readyAt=now+cooldown;}
    public void Reset() {active=0;readyAt=0;}
}
