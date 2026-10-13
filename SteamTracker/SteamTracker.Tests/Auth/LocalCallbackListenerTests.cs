using System.Net;
using SteamTracker.Integrations.Auth;

namespace SteamTracker.Tests.Auth;

public class LocalCallbackListenerTests
{
    [Fact]
    public async Task WaitForCallback_ReturnsDecodedQueryParameters()
    {
        var port = LocalPort.FindFree();
        using var listener = new LocalCallbackListener(port);
        listener.Start();

        var wait = listener.WaitForCallbackAsync(CancellationToken.None);

        using var http = new HttpClient();
        var response = await http.GetAsync($"http://localhost:{port}/callback?openid.mode=id_res&a=b%3Ac");
        var result = await wait;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("id_res", result["openid.mode"]);
        Assert.Equal("b:c", result["a"]);
    }

    [Fact]
    public async Task WaitForCallback_Cancelled_ThrowsAndReleasesPort()
    {
        var port = LocalPort.FindFree();
        using var cts = new CancellationTokenSource();

        using (var listener = new LocalCallbackListener(port))
        {
            listener.Start();
            var wait = listener.WaitForCallbackAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        }

        using var second = new LocalCallbackListener(port);
        second.Start(); 
    }
}