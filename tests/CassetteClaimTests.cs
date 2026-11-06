using DeepSpaceMusic;
static class CassetteClaimTests {
    sealed class Item {public bool Stored,Destroyed;}
    public static void Run(Action<bool,string> check) {
        int spawned=0,placed=0,discarded=0,errors=0;
        Item Spawn(){spawned++;return new Item();}
        void Place(Item item){placed++;item.Stored=true;}
        bool Stored(Item item)=>item.Stored;
        void Discard(Item item){discarded++;item.Destroyed=true;}
        void Report(Exception error){errors++;}
        var notReady=CassetteClaim.Give(false,Spawn,Place,Stored,Discard,Report);
        check(notReady==CassetteClaimResult.NotInGame && spawned==0 && placed==0 && discarded==0,"claim from the main menu gives guidance without spawning or placing an item");
        check(CassetteClaim.Message(notReady).Contains("载入存档"),"main-menu claim explains that media connection does not mean a save is loaded");
        var success=CassetteClaim.Give(true,Spawn,Place,Stored,Discard,Report);
        check(success==CassetteClaimResult.Claimed && placed==1 && discarded==0 && CassetteClaim.Message(success).Contains("下方背包"),"successful claim confirms the destination and preserves the placed cassette");
        var full=CassetteClaim.Give(true,Spawn,_=>{},Stored,Discard,Report);
        check(full==CassetteClaimResult.Full && discarded==1 && CassetteClaim.Message(full).Contains("空位"),"full inventory cleans up only the unplaced new cassette and explains how to retry");
        var missing=CassetteClaim.Give<Item>(true,()=>null,Place,Stored,Discard,Report);
        check(missing==CassetteClaimResult.SpawnFailed && placed==1 && discarded==1,"failed item initialization never attempts placement or reports a successful claim");
        var broken=CassetteClaim.Give<Item>(true,()=>throw new InvalidOperationException(),Place,Stored,Discard,Report);
        check(broken==CassetteClaimResult.Failed && errors==1 && discarded==1,"spawn exception becomes visible feedback without touching existing inventory");
        var afterPlace=new Item();
        var partial=CassetteClaim.Give(true,()=>afterPlace,item=>{item.Stored=true;throw new InvalidOperationException();},Stored,Discard,Report);
        check(partial==CassetteClaimResult.Failed && afterPlace.Stored && !afterPlace.Destroyed && discarded==1,"exception after placement cannot destroy an already stored cassette");
        var cleanup=CassetteClaim.Give(true,Spawn,_=>{},Stored,_=>throw new InvalidOperationException(),Report);
        check(cleanup==CassetteClaimResult.CleanupFailed && errors==3,"cleanup failure is reported instead of escaping the claim button callback");
    }
}
