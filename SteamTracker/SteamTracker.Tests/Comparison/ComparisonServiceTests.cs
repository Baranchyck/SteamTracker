using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Comparison;

namespace SteamTracker.Tests.Comparison;

public class ComparisonServiceTests
{
    private const string Me = "me";
    private const int Portal = 620;
    private const int Dota = 570;

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    private readonly Dictionary<string, IReadOnlyList<OwnedGame>?> _libraries = new();
    private readonly List<string> _friendIds = new();

    public ComparisonServiceTests()
    {
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(Me, "Me", "", ProfileVisibility.Public));

        _steamApi.Setup(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _libraries[id]);

        _steamApi.Setup(s => s.GetFriendIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _friendIds.ToList());

        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids
                .Select(id => new PlayerPresence(id, id, "", ProfileVisibility.Public, PersonaState.Online, null, null))
                .ToList());

        SetupMe(Game(Portal, 300), Game(Dota, 60));
    }

    private ComparisonService CreateService() => new(_steamApi.Object, _auth.Object);

    private static OwnedGame Game(int appId, int minutes, int twoWeeks = 0) =>
        new(appId, appId == Portal ? "Portal 2" : "Dota 2", minutes, twoWeeks, null, "");

    private void SetupMe(params OwnedGame[] games) => _libraries[Me] = games;

    // Друг з ніком = SteamID, щоб у тестах було видно, хто є хто
    private void AddFriend(string name, params OwnedGame[] games)
    {
        _friendIds.Add(name);
        _libraries[name] = games;
    }

    private void AddHiddenFriend(string name)
    {
        _friendIds.Add(name);
        _libraries[name] = null;
    }

    private static IEnumerable<(string, int)> NamesAndRanks(GameComparison c) =>
        c.Ranking.Select(e => (e.PersonaName, e.Rank));

    [Fact]
    public async Task GetComparison_RanksByPlaytimeAndFindsUserPosition()
    {
        AddFriend("Ann", Game(Portal, 600, twoWeeks: 30));
        AddFriend("Bob", Game(Portal, 100));

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Available, result.Status);
        var c = result.Value!;
        Assert.Equal("Portal 2", c.GameName);
        Assert.Equal(new[] { ("Ann", 1), ("Me", 2), ("Bob", 3) }, NamesAndRanks(c));
        Assert.Equal(2, c.UserRank);
        Assert.True(c.Ranking.Single(e => e.PersonaName == "Me").IsCurrentUser);
        Assert.Equal(30, c.Ranking.Single(e => e.PersonaName == "Ann").Playtime2WeeksMinutes);
    }

    [Fact]
    public async Task GetComparison_EqualPlaytime_SharesRank()
    {
        AddFriend("Ann", Game(Portal, 300));
        AddFriend("Bob", Game(Portal, 600));
        AddFriend("Cid", Game(Portal, 100));

        var result = await CreateService().GetComparisonAsync(Portal);

        // Me і Ann мають однаковий час — обоє на 2 місці, наступний — 4-й
        Assert.Equal(new[] { ("Bob", 1), ("Ann", 2), ("Me", 2), ("Cid", 4) }, NamesAndRanks(result.Value!));
        Assert.Equal(2, result.Value!.UserRank);
    }

    [Fact]
    public async Task GetComparison_PrivateLibrary_InSeparateListNotZeroHours()
    {
        AddFriend("Ann", Game(Portal, 600));
        AddHiddenFriend("Hidden");

        var c = (await CreateService().GetComparisonAsync(Portal)).Value!;

        Assert.Equal("Hidden", Assert.Single(c.FriendsWithPrivateLibrary).PersonaName);
        Assert.DoesNotContain(c.Ranking, e => e.PersonaName == "Hidden");
        Assert.Empty(c.FriendsWithoutGame);
    }

    [Fact]
    public async Task GetComparison_FriendWithoutGame_InSeparateList()
    {
        AddFriend("Ann", Game(Dota, 600));

        var c = (await CreateService().GetComparisonAsync(Portal)).Value!;

        Assert.Equal("Ann", Assert.Single(c.FriendsWithoutGame).PersonaName);
        Assert.Equal("Me", Assert.Single(c.Ranking).PersonaName);
    }

    [Fact]
    public async Task GetComparison_NoFriendHasGame_AverageNullWithoutException()
    {
        AddFriend("Ann", Game(Dota, 600));
        AddHiddenFriend("Hidden");

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Null(result.Value!.PercentOfAverage);
        Assert.Equal(1, result.Value!.UserRank);
    }

    [Fact]
    public async Task GetComparison_NoFriends_AvailableWithOnlyUser()
    {
        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal("Me", Assert.Single(result.Value!.Ranking).PersonaName);
        Assert.Null(result.Value!.PercentOfAverage);
    }

    [Fact]
    public async Task GetComparison_PercentOfAverage_OnlyFriendsWithPlaytime()
    {
        // Середнє серед друзів з часом > 0: (200 + 400) / 2 = 300; Ann з 0 хв не враховується
        AddFriend("Ann", Game(Portal, 0));
        AddFriend("Bob", Game(Portal, 200));
        AddFriend("Cid", Game(Portal, 400));

        var c = (await CreateService().GetComparisonAsync(Portal)).Value!;

        Assert.Equal(100m, c.PercentOfAverage);
        // Друг із грою, але 0 хв, — у рейтингу, просто внизу
        Assert.Equal(("Ann", 4), NamesAndRanks(c).Last());
    }

    [Fact]
    public async Task GetComparison_LoadsAtMostFourLibrariesInParallel()
    {
        for (int i = 0; i < 12; i++)
            AddFriend($"F{i}", Game(Portal, i * 10));

        int current = 0, max = 0;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string id, CancellationToken _) =>
            {
                int now = Interlocked.Increment(ref current);
                lock (_libraries) max = Math.Max(max, now);
                await Task.Delay(20);
                Interlocked.Decrement(ref current);
                return _libraries[id];
            });

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(13, result.Value!.Ranking.Count);
        Assert.InRange(max, 1, ComparisonService.MaxParallelRequests);
    }

    [Fact]
    public async Task GetComparison_SecondGameWithinTtl_DoesNotReloadLibraries()
    {
        AddFriend("Ann", Game(Portal, 600), Game(Dota, 10));
        AddFriend("Bob", Game(Portal, 100));
        var service = CreateService();

        await service.GetComparisonAsync(Portal);
        var dota = await service.GetComparisonAsync(Dota);

        Assert.Equal(DataStatus.Available, dota.Status);
        foreach (var id in new[] { Me, "Ann", "Bob" })
            _steamApi.Verify(s => s.GetOwnedGamesAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetComparison_FriendListHidden_ReturnsPrivate()
    {
        _steamApi.Setup(s => s.GetFriendIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>?)null);

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task GetComparison_FriendWithEmptyLibrary_InWithoutGameNotPrivate()
    {
        AddFriend("Ann", Game(Portal, 600));
        AddFriend("Empty");

        var c = (await CreateService().GetComparisonAsync(Portal)).Value!;

        Assert.Equal("Empty", Assert.Single(c.FriendsWithoutGame).PersonaName);
        Assert.Empty(c.FriendsWithPrivateLibrary);
    }

    [Fact]
    public async Task GetComparison_UserLibraryHidden_ReturnsPrivateWithoutLoadingFriends()
    {
        _libraries[Me] = null;
        AddFriend("Ann", Game(Portal, 600));

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
        _steamApi.Verify(s => s.GetFriendIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetComparison_GameNotInUserLibrary_ReturnsError()
    {
        var result = await CreateService().GetComparisonAsync(12345);

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task GetComparison_SteamApiException_ReturnsErrorWithoutDetails()
    {
        AddFriend("Ann", Game(Portal, 600));
        _steamApi.Setup(s => s.GetOwnedGamesAsync("Ann", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.DoesNotContain("boom", result.Message);
    }

    [Fact]
    public async Task GetComparison_NoCurrentUser_ReturnsErrorWithoutCallingSteam()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().GetComparisonAsync(Portal);

        Assert.Equal(DataStatus.Error, result.Status);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
