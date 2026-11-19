using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Matchmaker;

namespace SteamTracker.Tests.Matchmaker;

public class MatchmakerServiceTests
{
    private const string Me = "76561198000000001";
    private const string Ann = "76561198000000002";
    private const string Bob = "76561198000000003";
    private const string Cid = "76561198000000004";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<ISteamIdResolver> _resolver = new();

    // Хто з SteamID існує в Steam (повертається з GetPlayerSummaries)
    private readonly HashSet<string> _existing = new() { Me, Ann, Bob, Cid };

    public MatchmakerServiceTests()
    {
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(Me, "Me", "", ProfileVisibility.Public));

        // За замовчуванням ввід — це вже SteamID64; "bad…" — невалідний
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, CancellationToken _) => input.StartsWith("bad")
                ? SteamIdResolveResult.Fail(input, SteamIdResolveError.InvalidFormat, "Невірний формат.")
                : SteamIdResolveResult.Ok(input, input.Trim()));

        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids
                .Where(_existing.Contains)
                .Select(id => new PlayerPresence(id, $"p{id[^1]}", "", ProfileVisibility.Public,
                    PersonaState.Online, null, null))
                .ToList());

        SetupHiddenLibrary(Me);
        SetupHiddenLibrary(Ann);
        SetupHiddenLibrary(Bob);
        SetupHiddenLibrary(Cid);
    }

    private MatchmakerService CreateService() => new(_steamApi.Object, _auth.Object, _resolver.Object);

    private static OwnedGame Game(int id, string name, int minutes) => new(id, name, minutes, 0, null, "");

    private void SetupLibrary(string steamId, params OwnedGame[] games)
    {
        IReadOnlyList<OwnedGame> list = games;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(steamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private void SetupHiddenLibrary(string steamId) =>
        _steamApi.Setup(s => s.GetOwnedGamesAsync(steamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OwnedGame>?)null);

    [Fact]
    public async Task FindCommonGames_ThreeLibraries_IntersectionSortedByAveragePlaytime()
    {
        SetupLibrary(Me, Game(1, "Dota", 600), Game(2, "Portal", 60), Game(3, "Solo", 100), Game(4, "Rust", 0));
        SetupLibrary(Ann, Game(1, "Dota", 300), Game(2, "Portal", 600), Game(4, "Rust", 30));
        SetupLibrary(Bob, Game(1, "Dota", 0), Game(2, "Portal", 900), Game(4, "Rust", 0), Game(5, "Other", 10));

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann, Bob });

        Assert.Equal(DataStatus.Available, result.Status);
        var games = result.Value!.CommonGames;
        // Portal: (60+600+900)/3 = 520; Dota: 300; Rust: 10
        Assert.Equal(new[] { 2, 1, 4 }, games.Select(g => g.AppId));
        Assert.Equal(520m, games[0].AveragePlaytimeMinutes);
        Assert.Equal(600, games[0].PlaytimeMinutesBySteamId[Ann]);
        Assert.Equal(3, games[0].PlaytimeMinutesBySteamId.Count);
    }

    [Fact]
    public async Task FindCommonGames_EmptyIntersection_AvailableWithEmptyList()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann, Game(2, "B", 10));

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann });

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!.CommonGames);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task FindCommonGames_HiddenLibrary_ExcludedFromIntersectionAndListedSeparately()
    {
        SetupLibrary(Me, Game(1, "A", 10), Game(2, "B", 10));
        SetupLibrary(Ann, Game(1, "A", 10));
        SetupHiddenLibrary(Bob);

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann, Bob });

        var match = result.Value!;
        Assert.Equal(1, Assert.Single(match.CommonGames).AppId);
        var hidden = Assert.Single(match.HiddenLibraryParticipants);
        Assert.Equal(Bob, hidden.SteamId);
        Assert.True(match.Participants.Single(p => p.SteamId == Bob).LibraryHidden);
        Assert.False(match.CommonGames[0].PlaytimeMinutesBySteamId.ContainsKey(Bob));
    }

    [Fact]
    public async Task FindCommonGames_OnlyOneVisibleLibrary_EmptyListWithMessage()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupHiddenLibrary(Ann);

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann });

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!.CommonGames);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task FindCommonGames_EmptyLibrary_NotHiddenAndNoCommonGames()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann);

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann });

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!.CommonGames);
        Assert.Empty(result.Value.HiddenLibraryParticipants);
        Assert.False(result.Value.Participants.Single(p => p.SteamId == Ann).LibraryHidden);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task FindCommonGames_InvalidInput_ListedAndRestWorks()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann, Game(1, "A", 20));

        var result = await CreateService().FindCommonGamesAsync(new[] { "bad-link", Ann });

        var match = result.Value!;
        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal("bad-link", Assert.Single(match.InvalidInputs).Input);
        Assert.Equal(new[] { Me, Ann }, match.Participants.Select(p => p.SteamId));
        Assert.Single(match.CommonGames);
    }

    [Fact]
    public async Task FindCommonGames_NonexistentSteamId_MovedToInvalid()
    {
        const string ghost = "76561198000000099";
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann, Game(1, "A", 20));

        var result = await CreateService().FindCommonGamesAsync(new[] { ghost, Ann });

        var invalid = Assert.Single(result.Value!.InvalidInputs);
        Assert.Equal(ghost, invalid.Input);
        Assert.Equal(SteamIdResolveError.NotFound, invalid.Error);
        Assert.DoesNotContain(result.Value.Participants, p => p.SteamId == ghost);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(ghost, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCommonGames_DuplicatesAndSelf_Removed()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann, Game(1, "A", 20));

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann, Me, " " + Ann, Ann });

        var participants = result.Value!.Participants;
        Assert.Equal(new[] { Me, Ann }, participants.Select(p => p.SteamId));
        Assert.True(participants[0].IsCurrentUser);
        Assert.False(participants[1].IsCurrentUser);
        Assert.Empty(result.Value.InvalidInputs);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(Ann, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindCommonGames_OnlySelfInInput_ReturnsError()
    {
        var result = await CreateService().FindCommonGamesAsync(new[] { Me, "" });

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task FindCommonGames_AllInputsInvalid_ErrorWithReason()
    {
        var result = await CreateService().FindCommonGamesAsync(new[] { "bad1", "bad2" });

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.Contains("Невірний формат", result.Message);
    }

    [Fact]
    public async Task FindCommonGames_MoreThanEightParticipants_ReturnsError()
    {
        var ids = Enumerable.Range(10, 8).Select(i => $"765611980000000{i}").ToArray();
        foreach (var id in ids)
            _existing.Add(id);

        var result = await CreateService().FindCommonGamesAsync(ids);

        Assert.Equal(DataStatus.Error, result.Status);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCommonGames_TooManyInputs_ErrorWithoutSteamCalls()
    {
        var inputs = Enumerable.Repeat(Ann, MatchmakerService.MaxInputs + 1).ToArray();

        var result = await CreateService().FindCommonGamesAsync(inputs);

        Assert.Equal(DataStatus.Error, result.Status);
        _resolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCommonGames_ProfilesLoadedInOneBatch()
    {
        SetupLibrary(Me, Game(1, "A", 10));
        SetupLibrary(Ann, Game(1, "A", 10));
        SetupLibrary(Bob, Game(1, "A", 10));

        await CreateService().FindCommonGamesAsync(new[] { Ann, Bob });

        _steamApi.Verify(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindCommonGames_ParallelLibraryRequestsDoNotExceedLimit()
    {
        var ids = Enumerable.Range(10, 7).Select(i => $"765611980000000{i}").ToArray();
        foreach (var id in ids)
            _existing.Add(id);

        int current = 0, max = 0;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                int now = Interlocked.Increment(ref current);
                lock (_existing) max = Math.Max(max, now);
                await Task.Delay(20);
                Interlocked.Decrement(ref current);
                return (IReadOnlyList<OwnedGame>)new[] { Game(1, "A", 10) };
            });

        var result = await CreateService().FindCommonGamesAsync(ids);

        Assert.Equal(8, result.Value!.Participants.Count);
        Assert.InRange(max, 1, MatchmakerService.MaxParallelRequests);
    }

    [Fact]
    public async Task FindCommonGames_NoCurrentUser_ReturnsError()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann });

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task FindCommonGames_SteamApiFails_ReturnsError()
    {
        _steamApi.Setup(s => s.GetOwnedGamesAsync(Ann, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann });

        Assert.Equal(DataStatus.Error, result.Status);
    }
}
