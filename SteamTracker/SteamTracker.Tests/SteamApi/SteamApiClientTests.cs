using System.Net;
using System.Text;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.SteamApi;

namespace SteamTracker.Tests.SteamApi;

public class SteamApiClientTests
{
    private const string SteamId = "76561197960287930";

    // Підміняє відповідь Steam, щоб тести не ходили в мережу й не потребували справжнього ключа
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static (SteamApiClient Client, StubHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(status, body);
        return (new SteamApiClient(new HttpClient(handler), "TEST_KEY"), handler);
    }

    [Fact]
    public async Task GetPlayerSummary_MapsProfile()
    {
        var (client, handler) = Create($$$"""
            {"response":{"players":[{
                "steamid":"{{{SteamId}}}","personaname":"Gabe",
                "avatarfull":"https://avatars/gabe_full.jpg","communityvisibilitystate":3
            }]}}
            """);

        var profile = await client.GetPlayerSummaryAsync(SteamId);

        Assert.Equal(new SteamProfile(SteamId, "Gabe", "https://avatars/gabe_full.jpg", ProfileVisibility.Public), profile);
        Assert.Contains("GetPlayerSummaries", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains($"steamids={SteamId}", handler.LastRequestUri.Query);
    }

    [Theory]
    [InlineData(1, ProfileVisibility.Private)]
    [InlineData(2, ProfileVisibility.Limited)]
    [InlineData(3, ProfileVisibility.Public)]
    public async Task GetPlayerSummary_MapsVisibility(int state, ProfileVisibility expected)
    {
        var (client, _) = Create($$$"""
            {"response":{"players":[{"steamid":"{{{SteamId}}}","personaname":"x","avatarfull":"","communityvisibilitystate":{{{state}}}}]}}
            """);

        var profile = await client.GetPlayerSummaryAsync(SteamId);

        Assert.Equal(expected, profile!.Visibility);
    }

    [Fact]
    public async Task GetPlayerSummary_UnknownSteamId_ReturnsNull()
    {
        var (client, _) = Create("""{"response":{"players":[]}}""");

        Assert.Null(await client.GetPlayerSummaryAsync(SteamId));
    }

    [Fact]
    public async Task GetPlayerSummary_HttpError_ThrowsSteamApiException()
    {
        var (client, _) = Create("", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerSummaryAsync(SteamId));
    }

    [Fact]
    public async Task GetPlayerSummary_InvalidJson_ThrowsSteamApiException()
    {
        var (client, _) = Create("not json");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerSummaryAsync(SteamId));
    }
}
