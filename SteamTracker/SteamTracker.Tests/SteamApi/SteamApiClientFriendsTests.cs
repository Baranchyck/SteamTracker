using System.Net;
using System.Text;
using System.Web;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.SteamApi;

namespace SteamTracker.Tests.SteamApi;

public class SteamApiClientFriendsTests
{
    private const string SteamId = "76561197960287930";
    private const long FirstId = 76561197960287930;

    // На кожен запит відповідає функцією й запам'ятовує всі запити — потрібно для перевірки пачок
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _respond;

        public RecordingHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;

        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            var (status, body) = _respond(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(_exception);
    }

    private static (SteamApiClient Client, RecordingHandler Handler) Create(
        string body, HttpStatusCode status = HttpStatusCode.OK)
        => Create(_ => (status, body));

    private static (SteamApiClient Client, RecordingHandler Handler) Create(
        Func<HttpRequestMessage, (HttpStatusCode, string)> respond)
    {
        var handler = new RecordingHandler(respond);
        return (new SteamApiClient(new HttpClient(handler), "TEST_KEY"), handler);
    }

    private static string[] RequestedIds(Uri uri) =>
        HttpUtility.ParseQueryString(uri.Query)["steamids"]!.Split(',');

    // Відповідь GetPlayerSummaries з публічними онлайн-профілями для запитаних SteamID
    private static (HttpStatusCode, string) EchoSummaries(HttpRequestMessage request)
    {
        var players = RequestedIds(request.RequestUri!)
            .Select(id => $$$"""{"steamid":"{{{id}}}","personaname":"p{{{id}}}","avatarfull":"","communityvisibilitystate":3,"personastate":1}""");
        return (HttpStatusCode.OK, $$$"""{"response":{"players":[{{{string.Join(",", players)}}}]}}""");
    }

    // ---------- GetFriendIdsAsync ----------

    [Fact]
    public async Task GetFriendIds_ReturnsSteamIds()
    {
        var (client, handler) = Create("""
            {"friendslist":{"friends":[
                {"steamid":"76561197960265731","relationship":"friend","friend_since":0},
                {"steamid":"76561197960265738","relationship":"friend","friend_since":1700000000}
            ]}}
            """);

        var ids = await client.GetFriendIdsAsync(SteamId);

        Assert.Equal(new[] { "76561197960265731", "76561197960265738" }, ids);
        var uri = handler.Requests.Single();
        Assert.Contains("GetFriendList", uri.AbsolutePath);
        Assert.Contains($"steamid={SteamId}", uri.Query);
        Assert.Contains("relationship=friend", uri.Query);
    }

    [Fact]
    public async Task GetFriendIds_PrivateFriendList_401_ReturnsNull()
    {
        var (client, _) = Create("<html>Unauthorized</html>", HttpStatusCode.Unauthorized);

        Assert.Null(await client.GetFriendIdsAsync(SteamId));
    }

    [Theory]
    [InlineData("""{"friendslist":{"friends":[]}}""")]
    [InlineData("""{"friendslist":{}}""")]
    [InlineData("{}")]
    public async Task GetFriendIds_NoFriends_ReturnsEmptyList(string body)
    {
        var (client, _) = Create(body);

        var ids = await client.GetFriendIdsAsync(SteamId);

        Assert.NotNull(ids);
        Assert.Empty(ids);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)] // невалідний API-ключ — це помилка, а не приватність
    public async Task GetFriendIds_HttpError_ThrowsSteamApiException(HttpStatusCode status)
    {
        var (client, _) = Create("", status);

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetFriendIdsAsync(SteamId));
    }

    [Fact]
    public async Task GetFriendIds_InvalidJson_ThrowsSteamApiException()
    {
        var (client, _) = Create("not json");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetFriendIdsAsync(SteamId));
    }

    [Fact]
    public async Task GetFriendIds_NetworkError_ThrowsSteamApiException()
    {
        var client = new SteamApiClient(
            new HttpClient(new ThrowingHandler(new HttpRequestException("no network"))), "TEST_KEY");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetFriendIdsAsync(SteamId));
    }

    [Fact]
    public async Task GetFriendIds_Timeout_ThrowsSteamApiException()
    {
        var client = new SteamApiClient(
            new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout"))), "TEST_KEY");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetFriendIdsAsync(SteamId));
    }

    [Fact]
    public async Task GetFriendIds_CancelledByCaller_PropagatesCancellation()
    {
        var (client, _) = Create("{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetFriendIdsAsync(SteamId, cts.Token));
    }

    // ---------- GetPlayerSummariesAsync ----------

    [Fact]
    public async Task GetPlayerSummaries_250Ids_SendsThreeBatches_100_100_50()
    {
        var ids = Enumerable.Range(0, 250).Select(i => (FirstId + i).ToString()).ToList();
        var (client, handler) = Create(EchoSummaries);

        var result = await client.GetPlayerSummariesAsync(ids);

        Assert.Equal(new[] { 100, 100, 50 }, handler.Requests.Select(u => RequestedIds(u).Length));
        Assert.All(handler.Requests, u => Assert.Contains("GetPlayerSummaries", u.AbsolutePath));
        Assert.Equal(ids.OrderBy(x => x), result.Select(p => p.SteamId).OrderBy(x => x));
    }

    [Fact]
    public async Task GetPlayerSummaries_EmptyInput_SendsNoRequests()
    {
        var (client, handler) = Create(EchoSummaries);

        var result = await client.GetPlayerSummariesAsync(Array.Empty<string>());

        Assert.Empty(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPlayerSummaries_DuplicatesAndBlanks_RequestedOnce()
    {
        var (client, handler) = Create(EchoSummaries);

        var result = await client.GetPlayerSummariesAsync(new[] { SteamId, SteamId, "", "  " });

        Assert.Equal(new[] { SteamId }, RequestedIds(handler.Requests.Single()));
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPlayerSummaries_MapsPresenceInGame()
    {
        var (client, _) = Create($$$"""
            {"response":{"players":[{
                "steamid":"{{{SteamId}}}","personaname":"Gabe","avatarfull":"https://avatars/gabe_full.jpg",
                "communityvisibilitystate":3,"personastate":1,
                "gameid":"570","gameextrainfo":"Dota 2"
            }]}}
            """);

        var result = await client.GetPlayerSummariesAsync(new[] { SteamId });

        Assert.Equal(
            new PlayerPresence(SteamId, "Gabe", "https://avatars/gabe_full.jpg",
                ProfileVisibility.Public, PersonaState.Online, 570, "Dota 2"),
            result.Single());
    }

    [Fact]
    public async Task GetPlayerSummaries_MissingGameFields_MapsToNull()
    {
        // Офлайн / приватний профіль: немає personastate, gameid, gameextrainfo
        var (client, _) = Create($$$"""
            {"response":{"players":[{"steamid":"{{{SteamId}}}","personaname":"x","avatarfull":"","communityvisibilitystate":1}]}}
            """);

        var presence = (await client.GetPlayerSummariesAsync(new[] { SteamId })).Single();

        Assert.Null(presence.CurrentGameId);
        Assert.Null(presence.CurrentGameName);
        Assert.Equal(PersonaState.Offline, presence.PersonaState);
        Assert.Equal(ProfileVisibility.Private, presence.Visibility);
    }

    [Fact]
    public async Task GetPlayerSummaries_NonSteamGame_IdNullButNameKept()
    {
        var (client, _) = Create($$$"""
            {"response":{"players":[{"steamid":"{{{SteamId}}}","personaname":"x","avatarfull":"","communityvisibilitystate":3,
              "personastate":1,"gameid":"13281723458712322048","gameextrainfo":"Minecraft"}]}}
            """);

        var presence = (await client.GetPlayerSummariesAsync(new[] { SteamId })).Single();

        Assert.Null(presence.CurrentGameId);
        Assert.Equal("Minecraft", presence.CurrentGameName);
    }

    [Theory]
    [InlineData(0, PersonaState.Offline)]
    [InlineData(1, PersonaState.Online)]
    [InlineData(2, PersonaState.Busy)]
    [InlineData(3, PersonaState.Away)]
    [InlineData(4, PersonaState.Snooze)]
    [InlineData(5, PersonaState.LookingToTrade)]
    [InlineData(6, PersonaState.LookingToPlay)]
    [InlineData(42, PersonaState.Offline)] // невідоме значення від Steam не валить мапінг
    public async Task GetPlayerSummaries_MapsPersonaState(int state, PersonaState expected)
    {
        var (client, _) = Create($$$"""
            {"response":{"players":[{"steamid":"{{{SteamId}}}","personaname":"x","avatarfull":"","communityvisibilitystate":3,"personastate":{{{state}}}}]}}
            """);

        var presence = (await client.GetPlayerSummariesAsync(new[] { SteamId })).Single();

        Assert.Equal(expected, presence.PersonaState);
    }

    [Fact]
    public async Task GetPlayerSummaries_UnknownIds_AreSkipped()
    {
        var (client, _) = Create("""{"response":{"players":[]}}""");

        Assert.Empty(await client.GetPlayerSummariesAsync(new[] { SteamId }));
    }

    [Fact]
    public async Task GetPlayerSummaries_ErrorOnSecondBatch_ThrowsSteamApiException()
    {
        var ids = Enumerable.Range(0, 150).Select(i => (FirstId + i).ToString());
        var call = 0;
        var (client, _) = Create(req => ++call == 1 ? EchoSummaries(req) : (HttpStatusCode.InternalServerError, ""));

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerSummariesAsync(ids));
    }

    [Fact]
    public async Task GetPlayerSummaries_InvalidJson_ThrowsSteamApiException()
    {
        var (client, _) = Create("not json");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerSummariesAsync(new[] { SteamId }));
    }
}
