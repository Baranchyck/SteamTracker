using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Profile;

namespace SteamTracker.Tests.Profile;

public class ProfileStatusServiceTests
{
    private const string Me = "76561198000000001";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    private ProfileStatusService CreateService() => new(_steamApi.Object, _auth.Object);

    private void LogIn() =>
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(Me, "Me", "", ProfileVisibility.Public));

    private static PlayerPresence Presence(string steamId) =>
        new(steamId, "Me", "", ProfileVisibility.Public, PersonaState.Online, 570, "Dota 2");

    [Fact]
    public async Task GetCurrentStatusAsync_NotSignedIn_ReturnsNullWithoutCallingSteam()
    {
        var result = await CreateService().GetCurrentStatusAsync();

        Assert.Null(result);
        _steamApi.Verify(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_ReturnsCurrentUserPresence()
    {
        LogIn();
        IReadOnlyList<PlayerPresence> summaries = new[] { Presence(Me) };
        _steamApi.Setup(s => s.GetPlayerSummariesAsync(
                It.Is<IEnumerable<string>>(ids => ids.Single() == Me), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        var result = await CreateService().GetCurrentStatusAsync();

        Assert.NotNull(result);
        Assert.Equal(PersonaState.Online, result!.PersonaState);
        Assert.Equal("Dota 2", result.CurrentGameName);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_SteamReturnsNoProfile_ReturnsNull()
    {
        LogIn();
        IReadOnlyList<PlayerPresence> summaries = Array.Empty<PlayerPresence>();
        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        Assert.Null(await CreateService().GetCurrentStatusAsync());
    }

    [Fact]
    public async Task GetCurrentStatusAsync_SteamError_ReturnsNull()
    {
        LogIn();
        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("down"));

        Assert.Null(await CreateService().GetCurrentStatusAsync());
    }

    [Fact]
    public async Task GetCurrentStatusAsync_Cancelled_Throws()
    {
        LogIn();
        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().GetCurrentStatusAsync());
    }
}
