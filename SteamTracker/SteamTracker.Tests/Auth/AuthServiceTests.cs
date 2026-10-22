using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Auth;

namespace SteamTracker.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<ISteamOpenIdAuthenticator> _authenticator = new();
    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISettingsRepository> _settings = new();

    private AuthService CreateSut() =>
        new(_authenticator.Object, _steamApi.Object, _users.Object, _settings.Object);

    private static SteamProfile MakeProfile(string steamId = "123") =>
        new(steamId, "Denys", "http://avatar", default);

    [Fact]
    public async Task LoginAsync_Success_SavesProfileAndRaisesEventOnce()
    {
        var profile = MakeProfile();
        _authenticator
            .Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("123");
        _steamApi
            .Setup(s => s.GetPlayerSummaryAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var sut = CreateSut();
        var raised = 0;
        sut.CurrentUserChanged += (_, _) => raised++;

        var result = await sut.LoginAsync();

        Assert.Equal(profile, result);
        Assert.Equal(profile, sut.CurrentUser);
        Assert.Equal(1, raised);
        _users.Verify(u => u.UpsertAsync(profile), Times.Once);
        _settings.Verify(s => s.SetAsync(SettingsKeys.CurrentSteamId, "123"), Times.Once);
    }
    [Fact]
    public async Task LoginAsync_Cancelled_SavesNothingAndKeepsCurrentUser()
    {
        _authenticator
            .Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuthCancelledException("AuthCancelledException"));

        var sut = CreateSut();
        var raised = 0;
        sut.CurrentUserChanged += (_, _) => raised++;

        await Assert.ThrowsAsync<AuthCancelledException>(() => sut.LoginAsync());

        Assert.Null(sut.CurrentUser);
        Assert.Equal(0, raised);
        _users.Verify(u => u.UpsertAsync(It.IsAny<SteamProfile>()), Times.Never);
        _settings.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_NullProfile_ThrowsAuthFailedAndSavesNothing()
    {
        _authenticator
            .Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("123");
        _steamApi
            .Setup(s => s.GetPlayerSummaryAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SteamProfile?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<AuthFailedException>(() => sut.LoginAsync());

        Assert.Null(sut.CurrentUser);
        _users.Verify(u => u.UpsertAsync(It.IsAny<SteamProfile>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ApiThrows_WrapsInAuthFailedWithInner()
    {
        var inner = new HttpRequestException("boom");
        _authenticator
            .Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("123");
        _steamApi
            .Setup(s => s.GetPlayerSummaryAsync("123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<AuthFailedException>(() => sut.LoginAsync());

        Assert.Same(inner, ex.InnerException);
        _users.Verify(u => u.UpsertAsync(It.IsAny<SteamProfile>()), Times.Never);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WithStoredUser_SetsCurrentUserWithoutNetwork()
    {
        var profile = MakeProfile();
        _settings.Setup(s => s.GetAsync(SettingsKeys.CurrentSteamId)).ReturnsAsync("123");
        _users.Setup(u => u.GetAsync("123")).ReturnsAsync(profile);

        var sut = CreateSut();
        var raised = 0;
        sut.CurrentUserChanged += (_, _) => raised++;

        var result = await sut.TryRestoreSessionAsync();

        Assert.Equal(profile, result);
        Assert.Equal(profile, sut.CurrentUser);
        Assert.Equal(1, raised);
        _steamApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WithoutStoredUser_ReturnsNull()
    {
        _settings.Setup(s => s.GetAsync(SettingsKeys.CurrentSteamId)).ReturnsAsync((string?)null);

        var sut = CreateSut();
        var raised = 0;
        sut.CurrentUserChanged += (_, _) => raised++;

        var result = await sut.TryRestoreSessionAsync();

        Assert.Null(result);
        Assert.Null(sut.CurrentUser);
        Assert.Equal(0, raised);
        _steamApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LogoutAsync_DeletesSessionKeyClearsUserAndRaisesEvent()
    {
        var profile = MakeProfile();
        _settings.Setup(s => s.GetAsync(SettingsKeys.CurrentSteamId)).ReturnsAsync("123");
        _users.Setup(u => u.GetAsync("123")).ReturnsAsync(profile);

        var sut = CreateSut();
        await sut.TryRestoreSessionAsync();

        var raised = 0;
        sut.CurrentUserChanged += (_, _) => raised++;

        await sut.LogoutAsync();

        Assert.Null(sut.CurrentUser);
        Assert.Equal(1, raised);
        _settings.Verify(s => s.DeleteAsync(SettingsKeys.CurrentSteamId), Times.Once);
    }
}