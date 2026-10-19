using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace PSPad.App.Live;

public sealed class LiveStreamClient(HttpClient http) : ILiveStream
{
    public async Task<Stream> OpenAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/live");
        request.SetBrowserResponseStreamingEnabled(true);

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new HttpRequestException($"The live stream was refused ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }
}
