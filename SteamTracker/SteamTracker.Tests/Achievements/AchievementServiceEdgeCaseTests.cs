using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Achievements;

namespace SteamTracker.Tests.Achievements;

// Перехресна перевірка (#98): крайові випадки AchievementService (#80), яких немає в AchievementServiceTests
public class AchievementServiceEdgeCaseTests
{
    private const string SteamId = "76561198000000001";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    public AchievementServiceEdgeCaseTests()
    {
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(SteamId, "Tester", "", default));
    }

    private AchievementService CreateService() => new(_steamApi.Object, _auth.Object);

    private static OwnedGame Game(int id, string name) => new(id, name, 0, 0, null, "", HasCommunityVisibleStats: true);

    // n отриманих з total; ApiName — "a0", "a1", ...
    private static IReadOnlyList<PlayerAchievement> Achievements(int achieved, int total) =>
        Enumerable.Range(0, total)
            .Select(i => new PlayerAchievement($"a{i}", $"Ach {i}", null, i < achieved, null))
            .ToList();

    private static PlayerAchievement Ach(string api, bool achieved = false) => new(api, api, null, achieved, null);

    private void SetupGames(params OwnedGame[] games)
    {
        IReadOnlyList<OwnedGame> list = games;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(SteamId, It.IsAny<CancellationToken>())).ReturnsAsync(list);
    }

    private void SetupAchievements(int appId, IReadOnlyList<PlayerAchievement>? achievements) =>
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

    private void SetupGlobal(int appId, Dictionary<string, double> percents) =>
        _steamApi.Setup(s => s.GetGlobalAchievementPercentagesAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(percents);

    // План з total ачівок, з яких achieved отримано; решта — від найлегшої
    private static AchievementPlan Plan(int achieved, int total) =>
        new(1, achieved, total,
            Enumerable.Range(0, total - achieved)
                .Select(i => new PlannedAchievement($"r{i}", $"R {i:D2}", null, 100 - i))
                .ToList());

    // ---------- гра з однією ачівкою ----------

    [Fact]
    public async Task GetGamesInProgressAsync_SingleAchievementGames_NeverInProgress()
    {
        SetupGames(Game(1, "NotStarted"), Game(2, "Done"), Game(3, "Half"));
        SetupAchievements(1, Achievements(0, 1));
        SetupAchievements(2, Achievements(1, 1));
        SetupAchievements(3, Achievements(1, 2));

        var result = await CreateService().GetGamesInProgressAsync();

        // 0 з 1 — ще не почато, 1 з 1 — уже 100%: "в процесі" лише Half
        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal("Half", Assert.Single(result.Value!).Name);
    }

    [Fact]
    public async Task GetPlanAsync_SingleAchievementGame_OneRemaining()
    {
        SetupAchievements(1, new[] { Ach("only") });
        SetupGlobal(1, new() { ["only"] = 12.5 });

        var result = await CreateService().GetPlanAsync(1);

        var plan = result.Value!;
        Assert.Equal((0, 1, 0m), (plan.Achieved, plan.Total, plan.Percent));
        Assert.Equal(12.5, Assert.Single(plan.Remaining).GlobalPercent);
    }

    [Fact]
    public void BuildRoadmap_SingleAchievement_OnePointClosesGame()
    {
        // 1 пункт від 1 ачівки = 0,01 ачівки → треба цілу одну, і це одразу 100%
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 0, total: 1), 1);

        Assert.Single(roadmap.Achievements);
        Assert.Equal(100m, roadmap.NewPercent);
    }

    // ---------- без глобальних відсотків ----------

    [Fact]
    public async Task GetPlanAsync_NoGlobalPercentForAnyAchievement_SortedByNameWithoutMessage()
    {
        SetupAchievements(1, new[] { Ach("Charlie"), Ach("alpha"), Ach("Bravo"), Ach("Done", achieved: true) });
        // Steam відповів, але порожнім словником — це не збій, тож і повідомлення про збій не потрібне
        SetupGlobal(1, new());

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Null(result.Message);
        Assert.Equal(new[] { "alpha", "Bravo", "Charlie" }, result.Value!.Remaining.Select(a => a.DisplayName));
        Assert.All(result.Value.Remaining, a => Assert.Null(a.GlobalPercent));
    }

    // ---------- N = 0 і N > 100 ----------

    [Theory]
    [InlineData(0, 0, 20)]      // нічого не просили — нічого не додаємо
    [InlineData(100, 40, 100)]  // +100 пунктів з 20% — більше, ніж можна: усі 40 недосягнутих
    [InlineData(150, 40, 100)]
    public void BuildRoadmap_ZeroAndMoreThan100Points(int points, int expectedCount, int expectedPercent)
    {
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 10, total: 50), points);

        Assert.Equal(expectedCount, roadmap.Achievements.Count);
        Assert.Equal(20m, roadmap.CurrentPercent);
        Assert.Equal((decimal)expectedPercent, roadmap.NewPercent);
    }

    // Знайдено в #98: targetPointsUp * plan.Total множиться в int і переповнюється ще до ділення в decimal,
    // тож при великому N план мовчки порожній замість "усі ачівки, 100%". Прибрати Skip після виправлення #124
    [Fact(Skip = "Баг #124: переповнення int у AchievementService.BuildRoadmap при великому targetPointsUp")]
    public void BuildRoadmap_HugeTarget_TakesAllWithoutOverflow()
    {
        // Екран обмежує ввід (MaxLength = 3), але BuildRoadmap — публічний контракт IAchievementService
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 10, total: 50), int.MaxValue);

        Assert.Equal(40, roadmap.Achievements.Count);
        Assert.Equal(100m, roadmap.NewPercent);
    }

    [Fact]
    public void BuildRoadmap_GameWithoutAchievements_EmptyAndZeroPercent()
    {
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 0, total: 0), 150);

        Assert.Empty(roadmap.Achievements);
        Assert.Equal(0m, roadmap.CurrentPercent);
        Assert.Equal(0m, roadmap.NewPercent);
    }

    // ---------- гра вже на 100% ----------

    [Fact]
    public async Task GetPlanAsync_AllAchieved_EmptyRemainingAnd100Percent()
    {
        SetupAchievements(1, Achievements(3, 3));
        SetupGlobal(1, new());

        var plan = (await CreateService().GetPlanAsync(1)).Value!;

        Assert.Empty(plan.Remaining);
        Assert.Equal(100m, plan.Percent);
    }

    [Fact]
    public void BuildRoadmap_CompletedGame_EmptyAndStays100()
    {
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 50, total: 50), 10);

        Assert.Empty(roadmap.Achievements);
        Assert.Equal(100m, roadmap.CurrentPercent);
        Assert.Equal(100m, roadmap.NewPercent);
    }

    // ---------- скасування ----------

    [Fact]
    public async Task GetGamesInProgressAsync_CancelledDuringParallelLoad_ThrowsAndStopsRequests()
    {
        SetupGames(Enumerable.Range(1, 20).Select(i => Game(i, $"G{i}")).ToArray());

        using var cts = new CancellationTokenSource();
        int calls = 0;
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, int _, CancellationToken ct) =>
            {
                // Користувач пішов з екрана, поки вантажились перші ігри
                if (Interlocked.Increment(ref calls) == 2)
                    cts.Cancel();
                await Task.Delay(20, ct);
                return Achievements(1, 2);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().GetGamesInProgressAsync(cts.Token));

        // Після скасування нові запити не стартують: максимум ті, що вже були в роботі
        Assert.InRange(calls, 1, AchievementService.MaxParallelRequests);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_AlreadyCancelled_NoAchievementRequests()
    {
        SetupGames(Game(1, "G1"), Game(2, "G2"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().GetGamesInProgressAsync(cts.Token));

        _steamApi.Verify(s => s.GetPlayerAchievementsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------- некоректний ввід ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPlanAsync_InvalidAppId_ErrorWithoutSteamCalls(int appId)
    {
        var result = await CreateService().GetPlanAsync(appId);

        Assert.Equal(DataStatus.Error, result.Status);
        _steamApi.Verify(s => s.GetPlayerAchievementsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
