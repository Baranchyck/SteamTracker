using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.FriendsRadar;

namespace SteamTracker.Tests.FriendsRadar;

public class FriendsRadarServiceTests
{
    private const string Me = "76561198000000001";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    public FriendsRadarServiceTests()
    {
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(Me, "Me", "", ProfileVisibility.Public));
    }

    private FriendsRadarService CreateService() => new(_steamApi.Object, _auth.Object);

    private static PlayerPresence Friend(
        string name, PersonaState state, int? gameId = null, string? gameName = null) =>
        new($"id-{name}", name, "", ProfileVisibility.Public, state, gameId, gameName);

    private static PlayerPresence InGame(string name, PersonaState state = PersonaState.Online) =>
        Friend(name, state, gameId: 570, gameName: "Dota 2");

    private void SetupFriends(params PlayerPresence[] friends)
    {
        IReadOnlyList<string> ids = friends.Select(f => f.SteamId).ToList();
        _steamApi.Setup(s => s.GetFriendIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        IReadOnlyList<PlayerPresence> list = friends;
        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private static IEnumerable<string> Names(ServiceResult<IReadOnlyList<PlayerPresence>> result) =>
        result.Value!.Select(f => f.PersonaName);

    [Fact]
    public async Task GetFriendsAsync_OnlyAvailable_ExcludesOfflineAwayAndInGame()
    {
        SetupFriends(
            Friend("Online", PersonaState.Online),
            Friend("LookingToPlay", PersonaState.LookingToPlay),
            InGame("Playing"),
            Friend("Offline", PersonaState.Offline),
            Friend("Away", PersonaState.Away),
            Friend("Busy", PersonaState.Busy));

        var result = await CreateService().GetFriendsAsync(new RadarFilter(OnlyAvailable: true));

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal(new[] { "LookingToPlay", "Online" }, Names(result));
    }

    [Fact]
    public async Task GetFriendsAsync_NonSteamGame_CountsAsInGame()
    {
        // Для сторонньої гри Steam не дає AppId, лише назву
        SetupFriends(Friend("Modded", PersonaState.Online, gameId: null, gameName: "Minecraft"));

        var available = await CreateService().GetFriendsAsync(new RadarFilter(OnlyAvailable: true));

        Assert.Empty(available.Value!);
    }

    [Fact]
    public async Task GetFriendsAsync_SortsByGroupThenByName()
    {
        SetupFriends(
            Friend("Zed", PersonaState.Offline),
            Friend("Ann", PersonaState.Away),
            Friend("bob", PersonaState.Online),
            InGame("Cid", PersonaState.Busy),
            Friend("Amy", PersonaState.Offline),
            Friend("Bea", PersonaState.Busy),
            InGame("Abe"),
            Friend("Al", PersonaState.LookingToPlay));

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        // в грі → онлайн → відійшов/зайнятий → офлайн; всередині — за ніком без урахування регістру
        Assert.Equal(new[] { "Abe", "Cid", "Al", "bob", "Ann", "Bea", "Amy", "Zed" }, Names(result));
    }

    [Fact]
    public async Task GetFriendsAsync_NoFilter_ReturnsEveryone()
    {
        SetupFriends(
            Friend("Online", PersonaState.Online),
            Friend("Offline", PersonaState.Offline),
            InGame("Playing"));

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task GetFriendsAsync_FriendListHidden_ReturnsPrivate()
    {
        _steamApi.Setup(s => s.GetFriendIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>?)null);

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task GetFriendsAsync_NoFriends_AvailableEmptyWithoutSummariesCall()
    {
        SetupFriends();

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!);
        _steamApi.Verify(
            s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFriendsAsync_SteamApiException_ReturnsErrorWithoutDetails()
    {
        _steamApi.Setup(s => s.GetFriendIdsAsync(Me, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.NotNull(result.Message);
        Assert.DoesNotContain("boom", result.Message);
    }

    [Fact]
    public async Task GetFriendsAsync_NoCurrentUser_ReturnsErrorWithoutCallingSteam()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().GetFriendsAsync(new RadarFilter());

        Assert.Equal(DataStatus.Error, result.Status);
        _steamApi.Verify(
            s => s.GetFriendIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFriendsAsync_EveryCall_GoesToSteam()
    {
        // Без кешу: "Оновити" має показати свіжі статуси
        SetupFriends(Friend("Online", PersonaState.Online));
        var service = CreateService();

        await service.GetFriendsAsync(new RadarFilter());
        await service.GetFriendsAsync(new RadarFilter());

        _steamApi.Verify(
            s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
