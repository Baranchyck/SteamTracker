using Microsoft.Extensions.Logging;
using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Auth;
using SteamTracker.Services.Library;
using SteamTracker.Services.PricePerHour;

namespace SteamTracker.Tests.Logging;

// Перевіряємо рівні з #40: Error — виняток, Warning — очікуваний збій, Information — ключова подія
public class ServiceLoggingTests
{
    private static readonly SteamProfile User = new("76561198000000001", "Tester", "", ProfileVisibility.Public);

    private static void VerifyLogged<T>(Mock<ILogger<T>> logger, LogLevel level, Times times, bool withException = false)
    {
        logger.Verify(l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => !withException || e != null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    private static string LoggedText<T>(Mock<ILogger<T>> logger) =>
        string.Join("\n", logger.Invocations
            .Where(i => i.Method.Name == nameof(ILogger.Log))
            .Select(i => i.Arguments[2]?.ToString()));

    [Fact]
    public async Task Library_SteamApiException_LogsErrorWithException()
    {
        var steamApi = new Mock<ISteamApiClient>();
        var auth = new Mock<IAuthService>();
        var logger = new Mock<ILogger<LibraryService>>();
        auth.Setup(a => a.CurrentUser).Returns(User);
        steamApi.Setup(s => s.GetOwnedGamesAsync(User.SteamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("down", new HttpRequestException()));

        var result = await new LibraryService(steamApi.Object, auth.Object, logger.Object)
            .GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Error, result.Status);
        VerifyLogged(logger, LogLevel.Error, Times.Once(), withException: true);
    }

    [Fact]
    public async Task Library_PrivateProfile_LogsWarningWithoutSteamId()
    {
        var steamApi = new Mock<ISteamApiClient>();
        var auth = new Mock<IAuthService>();
        var logger = new Mock<ILogger<LibraryService>>();
        auth.Setup(a => a.CurrentUser).Returns(User);
        steamApi.Setup(s => s.GetOwnedGamesAsync(User.SteamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OwnedGame>());

        var result = await new LibraryService(steamApi.Object, auth.Object, logger.Object)
            .GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Private, result.Status);
        VerifyLogged(logger, LogLevel.Warning, Times.Once());
        VerifyLogged(logger, LogLevel.Error, Times.Never());
        Assert.DoesNotContain(User.SteamId, LoggedText(logger));
    }

    [Fact]
    public async Task Library_WithoutLogger_StillWorks()
    {
        var steamApi = new Mock<ISteamApiClient>();
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.CurrentUser).Returns(User);
        steamApi.Setup(s => s.GetOwnedGamesAsync(User.SteamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("down"));

        // Без логера (як у старих тестах) — NullLogger, без NullReferenceException
        var result = await new LibraryService(steamApi.Object, auth.Object).GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task PriceAnalyzer_StoreReturnsNoPrices_LogsWarning()
    {
        var library = new Mock<ILibraryService>();
        var store = new Mock<IStoreClient>();
        var logger = new Mock<ILogger<PriceAnalyzerService>>();
        IReadOnlyList<OwnedGame> games = new[] { new OwnedGame(10, "Game", 120, 0, null, "") };
        library.Setup(l => l.GetMyLibraryAsync(It.IsAny<LibraryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<IReadOnlyList<OwnedGame>>(games, DataStatus.Available));
        store.Setup(s => s.GetPricesAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GamePrice>());

        var result = await new PriceAnalyzerService(library.Object, store.Object, logger.Object).GetAnalysisAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        VerifyLogged(logger, LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Auth_Logout_LogsInformation()
    {
        var settings = new Mock<ISettingsRepository>();
        var logger = new Mock<ILogger<AuthService>>();
        var service = new AuthService(
            Mock.Of<ISteamOpenIdAuthenticator>(), Mock.Of<ISteamApiClient>(), Mock.Of<IUserRepository>(),
            settings.Object, logger.Object);

        await service.LogoutAsync();

        VerifyLogged(logger, LogLevel.Information, Times.Once());
    }
}
