using System.Net;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.SteamApi;

namespace SteamTracker.Tests.SteamApi;

public class SteamApiClientAchievementsTests
{
    private const string SteamId = "76561197960287930";
    private const int AppId = 620;

    private static (SteamApiClient Client, RecordingHandler Handler) Create(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new RecordingHandler(_ => (status, body));
        return (new SteamApiClient(new HttpClient(handler), "TEST_KEY"), handler);
    }

    // ---------- GetPlayerAchievementsAsync ----------

    [Fact]
    public async Task GetPlayerAchievements_MapsAchievedAndLocked()
    {
        var (client, handler) = Create("""
            {"playerstats":{"steamID":"76561197960287930","gameName":"Portal 2","success":true,"achievements":[
                {"apiname":"ACH.WAKE_UP","achieved":1,"unlocktime":1700000000,"name":"Прокинься","description":"Пройди перший рівень"},
                {"apiname":"ACH.SECRET","achieved":0,"unlocktime":0,"name":"Секрет","description":""}
            ]}}
            """);

        var result = await client.GetPlayerAchievementsAsync(SteamId, AppId);

        Assert.Equal(new[]
        {
            new PlayerAchievement("ACH.WAKE_UP", "Прокинься", "Пройди перший рівень", true,
                DateTimeOffset.FromUnixTimeSeconds(1700000000)),
            new PlayerAchievement("ACH.SECRET", "Секрет", null, false, null)
        }, result);

        var uri = handler.Requests.Single();
        Assert.Contains("GetPlayerAchievements", uri.AbsolutePath);
        Assert.Contains($"steamid={SteamId}", uri.Query);
        Assert.Contains($"appid={AppId}", uri.Query);
        Assert.Contains("l=ukrainian", uri.Query);
    }

    [Fact]
    public async Task GetPlayerAchievements_MissingName_FallsBackToApiName()
    {
        var (client, _) = Create("""
            {"playerstats":{"success":true,"achievements":[{"apiname":"ACH_1","achieved":0,"unlocktime":0}]}}
            """);

        var achievement = (await client.GetPlayerAchievementsAsync(SteamId, AppId))!.Single();

        Assert.Equal("ACH_1", achievement.DisplayName);
        Assert.Null(achievement.Description);
    }

    [Fact]
    public async Task GetPlayerAchievements_NoStats_400_ReturnsEmptyList()
    {
        var (client, _) = Create(
            """{"playerstats":{"error":"Requested app has no stats","success":false}}""",
            HttpStatusCode.BadRequest);

        var result = await client.GetPlayerAchievementsAsync(SteamId, AppId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlayerAchievements_StatsWithoutAchievements_ReturnsEmptyList()
    {
        var (client, _) = Create("""{"playerstats":{"steamID":"x","gameName":"y","success":true}}""");

        var result = await client.GetPlayerAchievementsAsync(SteamId, AppId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlayerAchievements_Private_403_ReturnsNull()
    {
        var (client, _) = Create(
            """{"playerstats":{"error":"Profile is not public","success":false}}""",
            HttpStatusCode.Forbidden);

        Assert.Null(await client.GetPlayerAchievementsAsync(SteamId, AppId));
    }

    [Fact]
    public async Task GetPlayerAchievements_SuccessFalseNotPublic_200_ReturnsNull()
    {
        var (client, _) = Create("""{"playerstats":{"error":"Profile is not public","success":false}}""");

        Assert.Null(await client.GetPlayerAchievementsAsync(SteamId, AppId));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"playerstats":{"error":"Invalid SteamID","success":false}}""")]
    [InlineData(HttpStatusCode.BadRequest, "not json")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task GetPlayerAchievements_RealErrors_ThrowSteamApiException(HttpStatusCode status, string body)
    {
        var (client, _) = Create(body, status);

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerAchievementsAsync(SteamId, AppId));
    }

    [Fact]
    public async Task GetPlayerAchievements_NetworkError_ThrowsSteamApiException()
    {
        var client = new SteamApiClient(
            new HttpClient(new ThrowingHandler(new HttpRequestException("no network"))), "TEST_KEY");

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetPlayerAchievementsAsync(SteamId, AppId));
    }

    // ---------- GetGlobalAchievementPercentagesAsync ----------

    [Fact]
    public async Task GetGlobalPercentages_ParsesNumberAndStringTheSame()
    {
        var (client, handler) = Create("""
            {"achievementpercentages":{"achievements":[
                {"name":"ACH_NUM","percent":12.5},
                {"name":"ACH_STR","percent":"12.5"}
            ]}}
            """);

        var result = await client.GetGlobalAchievementPercentagesAsync(AppId);

        Assert.Equal(12.5, result["ACH_NUM"]);
        Assert.Equal(12.5, result["ACH_STR"]);
        var uri = handler.Requests.Single();
        Assert.Contains("GetGlobalAchievementPercentagesForApp", uri.AbsolutePath);
        Assert.Contains($"gameid={AppId}", uri.Query);
    }

    [Theory]
    [InlineData("""{"achievementpercentages":{"achievements":[]}}""")]
    [InlineData("""{"achievementpercentages":{}}""")]
    [InlineData("{}")]
    public async Task GetGlobalPercentages_NoAchievements_ReturnsEmpty(string body)
    {
        var (client, _) = Create(body);

        Assert.Empty(await client.GetGlobalAchievementPercentagesAsync(AppId));
    }

    [Fact]
    public async Task GetGlobalPercentages_SkipsEntriesWithoutName()
    {
        var (client, _) = Create("""
            {"achievementpercentages":{"achievements":[{"name":"","percent":1},{"percent":2},{"name":"A","percent":3}]}}
            """);

        var result = await client.GetGlobalAchievementPercentagesAsync(AppId);

        Assert.Equal(new[] { "A" }, result.Keys);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task GetGlobalPercentages_Errors_ThrowSteamApiException(HttpStatusCode status, string body)
    {
        var (client, _) = Create(body, status);

        await Assert.ThrowsAsync<SteamApiException>(() => client.GetGlobalAchievementPercentagesAsync(AppId));
    }

    // ---------- GetOwnedGamesAsync: has_community_visible_stats ----------

    [Fact]
    public async Task GetOwnedGames_MapsHasCommunityVisibleStats()
    {
        var (client, _) = Create("""
            {"response":{"game_count":2,"games":[
                {"appid":620,"name":"Portal 2","playtime_forever":10,"has_community_visible_stats":true},
                {"appid":10,"name":"Counter-Strike","playtime_forever":5}
            ]}}
            """);

        var games = await client.GetOwnedGamesAsync(SteamId);

        Assert.NotNull(games);
        Assert.True(games!.Single(g => g.AppId == 620).HasCommunityVisibleStats);
        Assert.False(games.Single(g => g.AppId == 10).HasCommunityVisibleStats);
    }

    [Fact]
    public async Task GetOwnedGames_PrivateProfileWithoutGameCount_ReturnsNull()
    {
        var (client, _) = Create("""{"response":{}}""");

        var games = await client.GetOwnedGamesAsync(SteamId);

        Assert.Null(games);
    }

    [Fact]
    public async Task GetOwnedGames_OpenProfileWithoutGames_ReturnsEmptyNotNull()
    {
        var (client, _) = Create("""{"response":{"game_count":0}}""");

        var games = await client.GetOwnedGamesAsync(SteamId);

        Assert.NotNull(games);
        Assert.Empty(games!);
    }
}
