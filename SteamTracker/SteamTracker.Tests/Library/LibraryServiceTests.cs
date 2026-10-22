using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Library;

namespace SteamTracker.Tests.Library;

public class LibraryServiceTests
{
    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    private LibraryService CreateService() => new(_steamApi.Object, _auth.Object);

    private static SteamProfile User(string steamId = "76561198000000001") =>
        new(steamId, "Tester", "", default);

    private static OwnedGame Game(
        int id, string name, int forever = 0, DateTimeOffset? lastPlayed = null) =>
        new(id, name, forever, 0, lastPlayed, "");

    private void SetupLoggedInUserWithGames(params OwnedGame[] games)
    {
        var user = User();
        _auth.Setup(a => a.CurrentUser).Returns(user);

        IReadOnlyList<OwnedGame> list = games;
        _steamApi
            .Setup(s => s.GetOwnedGamesAsync(user.SteamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    [Fact]
    public async Task GetMyLibraryAsync_NoCurrentUser_ReturnsError()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.NotNull(result.Message);
        _steamApi.Verify(
            s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMyLibraryAsync_EmptyList_ReturnsPrivate()
    {
        SetupLoggedInUserWithGames();

        var result = await CreateService().GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetMyLibraryAsync_SteamApiException_ReturnsError()
    {
        var user = User();
        _auth.Setup(a => a.CurrentUser).Returns(user);
        _steamApi
            .Setup(s => s.GetOwnedGamesAsync(user.SteamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.NotNull(result.Message);
        Assert.DoesNotContain("boom", result.Message);
    }

    [Fact]
    public async Task GetMyLibraryAsync_HasGames_ReturnsAvailable()
    {
        SetupLoggedInUserWithGames(Game(1, "A"), Game(2, "B"));

        var result = await CreateService().GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal(2, result.Value!.Count);
    }
    [Fact]
    public async Task GetMyLibraryAsync_OnlyNeverPlayed_ReturnsOnlyZeroPlaytime()
    {
        SetupLoggedInUserWithGames(
            Game(1, "Played", forever: 120),
            Game(2, "Never", forever: 0),
            Game(3, "AlsoNever", forever: 0));

        var result = await CreateService()
            .GetMyLibraryAsync(new LibraryFilter(OnlyNeverPlayed: true));

        Assert.Equal(new[] { "AlsoNever", "Never" }, result.Value!.Select(g => g.Name));
    }

    [Fact]
    public async Task GetMyLibraryAsync_SortByName_IsCaseInsensitiveAscending()
    {
        SetupLoggedInUserWithGames(Game(1, "banana"), Game(2, "Apple"), Game(3, "cherry"));

        var result = await CreateService()
            .GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.NameAsc));

        Assert.Equal(new[] { "Apple", "banana", "cherry" }, result.Value!.Select(g => g.Name));
    }

    [Fact]
    public async Task GetMyLibraryAsync_SortByPlaytime_MostPlayedFirst()
    {
        SetupLoggedInUserWithGames(
            Game(1, "Low", forever: 10),
            Game(2, "High", forever: 300),
            Game(3, "Mid", forever: 50));

        var result = await CreateService()
            .GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.PlaytimeDesc));

        Assert.Equal(new[] { "High", "Mid", "Low" }, result.Value!.Select(g => g.Name));
    }

    [Fact]
    public async Task GetMyLibraryAsync_SortByLastPlayed_OldestFirstAndNullLast()
    {
        var old = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var recent = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        SetupLoggedInUserWithGames(
            Game(1, "Never", lastPlayed: null),
            Game(2, "Recent", lastPlayed: recent),
            Game(3, "Old", lastPlayed: old));

        var result = await CreateService()
            .GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.LastPlayedAsc));

        Assert.Equal(new[] { "Old", "Recent", "Never" }, result.Value!.Select(g => g.Name));
    }

    [Fact]
    public async Task GetMyLibraryAsync_SecondCallWithinTtl_DoesNotCallSteamAgain()
    {
        SetupLoggedInUserWithGames(Game(1, "A"), Game(2, "B"));
        var service = CreateService();

        await service.GetMyLibraryAsync(new LibraryFilter());
        await service.GetMyLibraryAsync(new LibraryFilter(OnlyNeverPlayed: true));

        _steamApi.Verify(
            s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}