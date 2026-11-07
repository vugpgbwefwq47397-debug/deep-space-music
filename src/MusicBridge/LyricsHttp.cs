using System.Text.Json;
namespace DeepSpaceMusic;

// Public metadata/lyrics only. Do not send music-client cookies or credentials.
public static class LyricsHttp {
    static readonly HttpClient direct=Create(false),system=Create(true);
    static HttpClient Create(bool useProxy) {
        var client=new HttpClient(new HttpClientHandler {UseCookies=false,UseProxy=useProxy}) {Timeout=Timeout.InfiniteTimeSpan};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DeepSpaceMusic/0.2.5 (+https://github.com/vugpgbwefwq47397-debug/deep-space-music)");
        return client;
    }
    public static async Task<JsonDocument> Read(string uri,CancellationToken token=default) {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var first=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);first.CancelAfter(TimeSpan.FromSeconds(4));
        try {return await ReadUsing(direct,uri,first.Token);}
        catch(Exception e) when(!timeout.IsCancellationRequested && (e is HttpRequestException or OperationCanceledException)) {
            // Retry through the user's existing system proxy; never edit proxy
            // settings, weaken certificate validation, or send client cookies.
            return await ReadUsing(system,uri,timeout.Token);
        }
    }
    static async Task<JsonDocument> ReadUsing(HttpClient client,string uri,CancellationToken token) {
        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        await using var stream=await response.Content.ReadAsStreamAsync(token);
        using var data=new MemoryStream();byte[] buffer=new byte[8192];int count;
        while((count=await stream.ReadAsync(buffer,token))>0) {if(data.Length+count>2_000_000)throw new JsonException("歌词响应过大");data.Write(buffer,0,count);}
        return JsonDocument.Parse(data.ToArray());
    }
}
