using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace DeepSpaceMusic;
public static class IpcClient {
    static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    public static async Task<string> Call(string request,CancellationToken token) {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(4500);
        await gate.WaitAsync(timeout.Token);
        try {
            using var pipe=new NamedPipeClientStream(".","DeepSpaceMusic.v1",PipeDirection.InOut,PipeOptions.Asynchronous);
            using var registration=timeout.Token.Register(()=>pipe.Dispose());
            await pipe.ConnectAsync(700,timeout.Token);
            using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
            using var reader=new StreamReader(pipe,new UTF8Encoding(false),false,1024,true);
            await writer.WriteLineAsync(request);
            var value=await reader.ReadLineAsync();if(value==null || value.Length>262144) throw new IOException("桥接响应无效");return value;
        } catch(Exception) when(timeout.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();throw new TimeoutException("桥接响应超时");
        } finally {gate.Release();}
    }
}
