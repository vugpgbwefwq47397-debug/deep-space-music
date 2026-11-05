using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace DeepSpaceMusic;
public static class IpcClient {
    static readonly RequestQueue queue=new RequestQueue();
    public static async Task<string> Call(string request,CancellationToken token,Func<bool> isCurrent=null) {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(4500);
        try {
            return await queue.Run(async ()=> {
            using var pipe=new NamedPipeClientStream(".","DeepSpaceMusic.v1",PipeDirection.InOut,PipeOptions.Asynchronous);
            using var registration=timeout.Token.Register(()=>pipe.Dispose());
            await pipe.ConnectAsync(700,timeout.Token);
            using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
            using var reader=new StreamReader(pipe,new UTF8Encoding(false),false,1024,true);
            if(isCurrent!=null && !isCurrent())throw new OperationCanceledException("操作已取消");
            await writer.WriteLineAsync(request);
            var value=await reader.ReadLineAsync();if(value==null || value.Length>262144) throw new IOException("桥接响应无效");return value;
            },timeout.Token,isCurrent);
        } catch(Exception) when(timeout.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();throw new TimeoutException("桥接响应超时");
        }
    }
}
