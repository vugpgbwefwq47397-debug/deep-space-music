using System;
using System.Threading;
using System.Threading.Tasks;
namespace DeepSpaceMusic;
public sealed class RequestQueue {
    readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    public async Task<string> Run(Func<Task<string>> send,CancellationToken token,Func<bool> isCurrent=null) {
        token.ThrowIfCancellationRequested();
        if(isCurrent!=null && !isCurrent())throw new OperationCanceledException("操作已取消");
        await gate.WaitAsync(token);
        try {
            token.ThrowIfCancellationRequested();
            // Navigation or platform selection may change while this request waits.
            if(isCurrent!=null && !isCurrent())throw new OperationCanceledException("操作已取消");
            return await send();
        } finally {gate.Release();}
    }
}
