using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Achievements;

namespace SteamTracker.Tests.Achievements;

public class AchievementServiceTests
{
    private const string SteamId = "76561198000000001";

    private readonly Mock<ISteamApiClient> _steamApi = new();
    private readonly Mock<IAuthService> _auth = new();

    private AchievementService CreateService() => new(_steamApi.Object, _auth.Object);

    private static OwnedGame Game(int id, string name, bool hasStats = true) =>
        new(id, name, 0, 0, null, "", hasStats);

    // n отриманих з total; ApiName — "a0", "a1", ...
    private static IReadOnlyList<PlayerAchievement> Achievements(int achieved, int total) =>
        Enumerable.Range(0, total)
            .Select(i => new PlayerAchievement($"a{i}", $"Ach {i}", null, i < achieved, null))
            .ToList();

    private static PlayerAchievement Ach(string api, bool achieved = false) =>
        new(api, api, null, achieved, null);

    private void LogIn() =>
        _auth.Setup(a => a.CurrentUser).Returns(new SteamProfile(SteamId, "Tester", "", default));

    private void SetupGames(params OwnedGame[] games)
    {
        IReadOnlyList<OwnedGame> list = games;
        _steamApi.Setup(s => s.GetOwnedGamesAsync(SteamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private void SetupAchievements(int appId, IReadOnlyList<PlayerAchievement>? achievements) =>
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

    private void SetupGlobal(int appId, Dictionary<string, double> percents) =>
        _steamApi.Setup(s => s.GetGlobalAchievementPercentagesAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(percents);

    private static AchievementPlan Plan(int achieved, int total) =>
        new(1, achieved, total,
            Enumerable.Range(0, total - achieved)
                .Select(i => new PlannedAchievement($"r{i}", $"R {i:D2}", null, 100 - i))
                .ToList());

    // ---------- GetGamesInProgressAsync ----------

    [Fact]
    public async Task GetGamesInProgressAsync_NoCurrentUser_ReturnsError()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_HiddenLibrary_ReturnsPrivate()
    {
        LogIn();
        _steamApi.Setup(s => s.GetOwnedGamesAsync(SteamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OwnedGame>?)null);

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_EmptyLibrary_ReturnsAvailableEmpty()
    {
        LogIn();
        SetupGames();

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_SkipsCompletedAndZeroAndGamesWithoutStats()
    {
        LogIn();
        SetupGames(
            Game(1, "InProgress"),
            Game(2, "Completed"),
            Game(3, "NotStarted"),
            Game(4, "NoAchievements"),
            Game(5, "NoStats", hasStats: false));
        SetupAchievements(1, Achievements(3, 10));
        SetupAchievements(2, Achievements(10, 10));
        SetupAchievements(3, Achievements(0, 10));
        SetupAchievements(4, Array.Empty<PlayerAchievement>());

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        var game = Assert.Single(result.Value!);
        Assert.Equal(1, game.AppId);
        Assert.Equal(3, game.Achieved);
        Assert.Equal(10, game.Total);
        Assert.Equal(30m, game.Percent);
        // Для гри без статистики в Steam навіть не ходимо
        _steamApi.Verify(s => s.GetPlayerAchievementsAsync(SteamId, 5, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_SortedByPercentDescending()
    {
        LogIn();
        SetupGames(Game(1, "Low"), Game(2, "High"), Game(3, "Mid"));
        SetupAchievements(1, Achievements(1, 10));
        SetupAchievements(2, Achievements(9, 10));
        SetupAchievements(3, Achievements(5, 10));

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(new[] { 2, 3, 1 }, result.Value!.Select(g => g.AppId));
    }

    [Fact]
    public async Task GetGamesInProgressAsync_AllAchievementsPrivate_ReturnsPrivate()
    {
        LogIn();
        SetupGames(Game(1, "A"), Game(2, "B"));
        SetupAchievements(1, null);
        SetupAchievements(2, null);

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_SteamApiFails_ReturnsError()
    {
        LogIn();
        SetupGames(Game(1, "A"));
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_ParallelRequestsDoNotExceedLimit()
    {
        LogIn();
        var games = Enumerable.Range(1, 20).Select(i => Game(i, $"G{i}")).ToArray();
        SetupGames(games);

        int current = 0, max = 0;
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                int now = Interlocked.Increment(ref current);
                InterlockedMax(ref max, now);
                await Task.Delay(20);
                Interlocked.Decrement(ref current);
                return Achievements(1, 2);
            });

        var result = await CreateService().GetGamesInProgressAsync();

        Assert.Equal(20, result.Value!.Count);
        Assert.InRange(max, 1, AchievementService.MaxParallelRequests);
    }

    [Fact]
    public async Task GetGamesInProgressAsync_SecondCall_UsesCache()
    {
        LogIn();
        SetupGames(Game(1, "A"));
        SetupAchievements(1, Achievements(1, 2));
        var service = CreateService();

        await service.GetGamesInProgressAsync();
        await service.GetGamesInProgressAsync();

        _steamApi.Verify(s => s.GetOwnedGamesAsync(SteamId, It.IsAny<CancellationToken>()), Times.Once);
        _steamApi.Verify(s => s.GetPlayerAchievementsAsync(SteamId, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetPlanAsync ----------

    [Fact]
    public async Task GetPlanAsync_NoCurrentUser_ReturnsError()
    {
        _auth.Setup(a => a.CurrentUser).Returns((SteamProfile?)null);

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task GetPlanAsync_PrivateStats_ReturnsPrivate()
    {
        LogIn();
        SetupAchievements(1, null);

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Private, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPlanAsync_SteamApiFails_ReturnsError()
    {
        LogIn();
        _steamApi.Setup(s => s.GetPlayerAchievementsAsync(SteamId, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task GetPlanAsync_GameWithoutAchievements_AvailableWithEmptyPlan()
    {
        LogIn();
        SetupAchievements(1, Array.Empty<PlayerAchievement>());

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Equal(0, result.Value!.Total);
        Assert.Empty(result.Value.Remaining);
        Assert.Equal(0m, result.Value.Percent);
        _steamApi.Verify(s => s.GetGlobalAchievementPercentagesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPlanAsync_RemainingSortedByGlobalPercent_NullLast()
    {
        LogIn();
        SetupAchievements(1, new[]
        {
            Ach("done", achieved: true),
            Ach("hard"),
            Ach("unknown"),
            Ach("easy"),
            Ach("mid"),
        });
        SetupGlobal(1, new Dictionary<string, double>
        {
            ["done"] = 90, ["hard"] = 1.5, ["easy"] = 70, ["mid"] = 30
        });

        var result = await CreateService().GetPlanAsync(1);

        var plan = result.Value!;
        Assert.Equal(new[] { "easy", "mid", "hard", "unknown" }, plan.Remaining.Select(a => a.ApiName));
        Assert.Null(plan.Remaining[^1].GlobalPercent);
        Assert.Equal(1, plan.Achieved);
        Assert.Equal(5, plan.Total);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task GetPlanAsync_GlobalPercentagesFail_PlanWithoutPercentsAndMessage()
    {
        LogIn();
        SetupAchievements(1, new[] { Ach("b"), Ach("a") });
        _steamApi.Setup(s => s.GetGlobalAchievementPercentagesAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateService().GetPlanAsync(1);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.NotNull(result.Message);
        Assert.All(result.Value!.Remaining, a => Assert.Null(a.GlobalPercent));
        Assert.Equal(new[] { "a", "b" }, result.Value.Remaining.Select(a => a.ApiName));
    }

    [Fact]
    public async Task GetPlanAsync_GlobalPercentagesCachedPerGame()
    {
        LogIn();
        SetupAchievements(1, new[] { Ach("a") });
        SetupGlobal(1, new Dictionary<string, double> { ["a"] = 10 });
        var service = CreateService();

        await service.GetPlanAsync(1);
        await service.GetPlanAsync(1);

        _steamApi.Verify(s => s.GetGlobalAchievementPercentagesAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- BuildRoadmap ----------

    [Fact]
    public void BuildRoadmap_50Total10Achieved_Plus10Points_Needs5Easiest()
    {
        var plan = Plan(achieved: 10, total: 50);

        var roadmap = CreateService().BuildRoadmap(plan, 10);

        Assert.Equal(5, roadmap.Achievements.Count);
        Assert.Equal(plan.Remaining.Take(5).Select(a => a.ApiName), roadmap.Achievements.Select(a => a.ApiName));
        Assert.Equal(20m, roadmap.CurrentPercent);
        Assert.Equal(30m, roadmap.NewPercent);
    }

    [Fact]
    public void BuildRoadmap_FractionalAchievement_RoundsUp()
    {
        // 1 пункт від 30 ачівок = 0,3 ачівки → треба 1
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 0, total: 30), 1);

        Assert.Single(roadmap.Achievements);
    }

    [Fact]
    public void BuildRoadmap_MorePointsThanRemaining_TakesAllAnd100Percent()
    {
        var plan = Plan(achieved: 10, total: 50);

        var roadmap = CreateService().BuildRoadmap(plan, 95);

        Assert.Equal(40, roadmap.Achievements.Count);
        Assert.Equal(100m, roadmap.NewPercent);
    }

    [Fact]
    public void BuildRoadmap_ZeroPoints_Empty()
    {
        var roadmap = CreateService().BuildRoadmap(Plan(achieved: 10, total: 50), 0);

        Assert.Empty(roadmap.Achievements);
        Assert.Equal(roadmap.CurrentPercent, roadmap.NewPercent);
    }

    [Fact]
    public void BuildRoadmap_NegativePoints_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().BuildRoadmap(Plan(1, 2), -1));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int initial;
        do
        {
            initial = target;
            if (value <= initial) return;
        } while (Interlocked.CompareExchange(ref target, value, initial) != initial);
    }
}
