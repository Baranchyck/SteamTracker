using System.Net;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Integrations.SteamApi;

namespace SteamTracker.Tests.SteamApi;

public class SteamApiClientVanityTests
{
    private static (SteamApiClient Client, RecordingHandler Handler) Create(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new RecordingHandler(_ => (status, body));
        return (new SteamApiClient(new HttpClient(handler), "TEST_KEY"), handler);
    }

    [Fact]
    public async Task ResolveVanityUrl_Success_ReturnsSteamId()
    {
        var (client, handler) = Create("""{"response":{"steamid":"76561197960287930","success":1}}""");

        var id = await client.ResolveVanityUrlAsync("gabelogannewell");

        Assert.Equal("76561197960287930", id);
        var uri = handler.Requests.Single();
        Assert.Contains("ResolveVanityURL", uri.AbsolutePath);
        Assert.Contains("vanityurl=gabelogannewell", uri.Query);
    }

    [Fact]
    public async Task ResolveVanityUrl_NoMatch42_ReturnsNull()
    {
        var (client, _) = Create("""{"response":{"success":42,"message":"No match"}}""");

        Assert.Null(await client.ResolveVanityUrlAsync("nosuchuser"));
    }

    [Fact]
    public async Task ResolveVanityUrl_EscapesInput()
    {
        var (client, handler) = Create("""{"response":{"success":42}}""");

        await client.ResolveVanityUrlAsync("a&key=x");

        Assert.Contains("vanityurl=a%26key%3Dx", handler.Requests.Single().Query);
    }

    [Theory]
    [InlineData("""{"response":{"success":8,"message":"Invalid URL"}}""")]
    [InlineData("""{"response":{"success":1}}""")]
    [InlineData("""{}""")]
    public async Task ResolveVanityUrl_UnexpectedResponse_Throws(string body)
    {
        var (client, _) = Create(body);

        await Assert.ThrowsAsync<SteamApiException>(() => client.ResolveVanityUrlAsync("x"));
    }

    [Fact]
    public async Task ResolveVanityUrl_ServerError_Throws()
    {
        var (client, _) = Create("oops", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<SteamApiException>(() => client.ResolveVanityUrlAsync("x"));
    }
}
