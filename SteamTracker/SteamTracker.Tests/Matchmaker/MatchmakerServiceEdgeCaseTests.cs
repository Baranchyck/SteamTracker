using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Matchmaker;

namespace SteamTracker.Tests.Matchmaker;

// Перехресна перевірка (#98): крайові випадки MatchmakerService (#87), яких немає в MatchmakerServiceTests
public class MatchmakerServiceEdgeCaseTests
{
    private const string Me = "76561198000000001";
    private const string Ann = "76561198000000002";
    private const string Bob = "76561198000000003";
    private const string ProfileUrl = "https://steamcommunity.com/profiles/";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<ISteamIdResolver> _resolver = new();

    // Хто з SteamID існує в Steam (повертається з GetPlayerSummaries)
    private readonly HashSet<string> _existing = new() { Me, Ann, Bob };

    public MatchmakerServiceEdgeCaseTests()
    {
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(Me, "Me", "", ProfileVisibility.Public));

        // Ввід — SteamID64 або посилання на профіль; з посилання береться SteamID у кінці
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, CancellationToken _) => SteamIdResolveResult.Ok(
                input, input.StartsWith(ProfileUrl) ? input[ProfileUrl.Length..].TrimEnd('/') : input.Trim()));

        _steamApi.Setup(s => s.GetPlayerSummariesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids
                .Where(_existing.Contains)
                .Select(id => new PlayerPresence(id, $"p{id[^2..]}", "", ProfileVisibility.Public,
                    PersonaState.Online, null, null))
                .ToList());

        // За замовчуванням у кожного є одна спільна гра
        _steamApi.Setup(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OwnedGame>)new[] { Game(1, "A", 10) });
    }

    private MatchmakerService CreateService() => new(_steamApi.Object, _auth.Object, _resolver.Object);

    private static OwnedGame Game(int id, string name, int minutes) => new(id, name, minutes, 0, null, "");

    private void SetupLibrary(string steamId, params OwnedGame[] games)
    {
        IReadOnlyList<OwnedGame> list = games;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(steamId, It.IsAny<CancellationToken>())).ReturnsAsync(list);
    }

    // count існуючих у Steam SteamID, крім Me, Ann, Bob
    private string[] Friends(int count)
    {
        var ids = Enumerable.Range(10, count).Select(i => $"765611980000000{i}").ToArray();
        foreach (var id in ids)
            _existing.Add(id);
        return ids;
    }

    // ---------- усі бібліотеки приховані ----------

    [Fact]
    public async Task FindCommonGames_AllLibrariesHidden_AvailableEmptyWithMessage()
    {
        // TODO(#93): порожній список = прихована бібліотека
        SetupLibrary(Me);
        SetupLibrary(Ann);
        SetupLibrary(Bob);

        var result = await CreateService().FindCommonGamesAsync(new[] { Ann, Bob });

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.NotNull(result.Message);
        Assert.Empty(result.Value!.CommonGames);
        Assert.All(result.Value.Participants, p => Assert.True(p.LibraryHidden));
        Assert.Equal(3, result.Value.HiddenLibraryParticipants.Count);
    }

    // ---------- межа в 8 учасників ----------

    [Fact]
    public async Task FindCommonGames_ExactlyEightParticipants_Allowed()
    {
        // 7 друзів + поточний користувач = рівно максимум
        var result = await CreateService().FindCommonGamesAsync(Friends(IMatchmakerService.MaxParticipants - 1));

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal(IMatchmakerService.MaxParticipants, result.Value!.Participants.Count);
        Assert.Single(result.Value.CommonGames);
    }

    [Fact]
    public async Task FindCommonGames_NineParticipants_ErrorWithoutLoadingLibraries()
    {
        var result = await CreateService().FindCommonGamesAsync(Friends(IMatchmakerService.MaxParticipants));

        Assert.Equal(DataStatus.Error, result.Status);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindCommonGames_EightPlusInvalidInputs_InvalidOnesDoNotCountTowardsLimit()
    {
        // Невалідні вводи не стають учасниками, тож 7 валідних друзів + 3 помилки — це все ще 8 учасників
        _resolver.Setup(r => r.ResolveAsync(It.Is<string>(s => s.StartsWith("bad")), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, CancellationToken _) =>
                SteamIdResolveResult.Fail(input, SteamIdResolveError.InvalidFormat, "Невірний формат."));

        var inputs = Friends(IMatchmakerService.MaxParticipants - 1).Concat(new[] { "bad1", "bad2", "bad3" }).ToArray();
        var result = await CreateService().FindCommonGamesAsync(inputs);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal(IMatchmakerService.MaxParticipants, result.Value!.Participants.Count);
        Assert.Equal(3, result.Value.InvalidInputs.Count);
    }

    // ---------- ввід із самих пробілів ----------

    [Theory]
    [InlineData("   ")]
    [InlineData(" ", "\t", "")]
    public async Task FindCommonGames_OnlyWhitespaceInputs_ErrorWithoutSteamCalls(params string[] inputs)
    {
        var result = await CreateService().FindCommonGamesAsync(inputs);

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.Contains("хоча б одного", result.Message);
        Assert.Empty(result.Value?.InvalidInputs ?? Array.Empty<SteamIdResolveResult>());
        _resolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- той самий гравець різними форматами ----------

    [Fact]
    public async Task FindCommonGames_SameFriendAsIdAndProfileUrl_CountedOnce()
    {
        var result = await CreateService().FindCommonGamesAsync(new[] { Ann, ProfileUrl + Ann + "/" });

        Assert.Equal(new[] { Me, Ann }, result.Value!.Participants.Select(p => p.SteamId));
        Assert.Empty(result.Value.InvalidInputs);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(Ann, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindCommonGames_OwnProfileUrlInInput_UserNotDuplicated()
    {
        var result = await CreateService().FindCommonGamesAsync(new[] { ProfileUrl + Me, Ann });

        var participants = result.Value!.Participants;
        Assert.Equal(new[] { Me, Ann }, participants.Select(p => p.SteamId));
        Assert.Single(participants, p => p.IsCurrentUser);
        _steamApi.Verify(s => s.GetOwnedGamesAsync(Me, It.IsAny<CancellationToken>()), Times.Once);
    }
}
