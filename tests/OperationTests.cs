using DeepSpaceMusic;
using System.Text.Json;
static class OperationTests {
    public static async Task Run(Action<bool,string> check) {
        var connection=new ConnectionPolicy();
        connection.Restore(false,true);connection.Request(true);
        check(connection.Enabled && connection.Requested && !connection.AutoReconnect,"manual Connect works with automatic reconnect disabled");
        connection.Lost();check(!connection.Requested && connection.Enabled,"loss with auto reconnect disabled retains intent but stops retrying");
        connection.Configure(true);check(connection.Requested,"enabling auto reconnect resumes an interrupted enabled connection");
        connection.Disconnect();connection.Configure(false);connection.Configure(true);
        check(!connection.Requested && !connection.Enabled,"auto reconnect toggle cannot undo manual Disconnect");
        connection.Restore(true,false);
        check(!connection.Requested && connection.AutoReconnect,"restart respects manual Disconnect with auto reconnect enabled");
        connection.Request(true);connection.Lost();check(connection.Requested && connection.Enabled,"explicit reconnect restores automatic recovery after later loss");
        var oldConfig=JsonSerializer.Deserialize<Request>("""{"platform":"qq","autoReconnect":true}""",new JsonSerializerOptions {IncludeFields=true});
        check(oldConfig.connectionEnabled,"old configuration retains its existing automatic connection behavior");
        var pending=new PendingOperation(.25);
        check(pending.TryBegin(10,out int first) && !pending.TryBegin(10.01,out _),"fast repeated clicks create only one in-flight playback operation");
        pending.Complete(first,11);
        int second=0;
        check(!pending.TryBegin(11.1,out _) && pending.TryBegin(11.3,out second),"short playback debounce lets media state catch up before accepting another click");
        pending.Reset();pending.TryBegin(12,out int third);pending.Complete(second,12);
        check(!pending.TryBegin(12.1,out _),"completion from before a platform switch cannot unlock a newer operation");
        pending.Complete(third,12.2);
        check(pending.TryBegin(12.5,out _),"the matching completion releases controls without leaving them stuck");
        var opening=new PendingOperation();opening.TryBegin(0,out int oldOpen);opening.Reset();opening.TryBegin(1,out int newOpen);opening.Complete(oldOpen,2);
        check(!opening.TryBegin(2,out _),"a late client-open response cannot release a newer client-open request");
        opening.Complete(newOpen,2);check(opening.TryBegin(2,out _),"client-open failure or completion allows a fresh retry");
        var queue=new RequestQueue();var entered=new TaskCompletionSource<bool>();var release=new TaskCompletionSource<bool>();
        var firstRequest=queue.Run(async ()=>{entered.SetResult(true);await release.Task;return "first";},CancellationToken.None);
        await entered.Task;
        bool current=true,dispatched=false;
        var obsolete=queue.Run(()=>{dispatched=true;return Task.FromResult("obsolete");},CancellationToken.None,()=>current);
        current=false;release.SetResult(true);await firstRequest;
        bool cancelled=false;try {await obsolete;}catch(OperationCanceledException){cancelled=true;}
        check(cancelled && !dispatched,"queued requests cancelled by navigation or platform change are never dispatched");
        bool failed=false;try {await queue.Run(()=>Task.FromException<string>(new IOException("transport failed")),CancellationToken.None);}catch(IOException){failed=true;}
        check(failed && await queue.Run(()=>Task.FromResult("healthy"),CancellationToken.None)=="healthy","failed transport releases the queue for later retry");
    }
}
