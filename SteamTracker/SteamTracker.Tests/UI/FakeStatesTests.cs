using SteamTracker.Domain.Models;
using SteamTracker.UI.Fakes;

namespace SteamTracker.Tests.UI;

/// <summary>
/// Стани екранів «дані / порожньо / приватний / помилка» на рівні сервісів: екран показує те, що повертає фейк,
/// тож якщо фейк віддає правильний Status і дані, перемикач у Debug-панелі екрана не збреше.
/// </summary>
public abstract class FakeStatesBase
{
    public enum Scenario { Data, Empty, Private, Error }

    protected static DataStatus StatusOf(Scenario s) => s switch
    {
        Scenario.Private => DataStatus.Private,
        Scenario.Error => DataStatus.Error,
        _ => DataStatus.Available
    };

    protected static DataStatus ExpectedStatus(Scenario s) => StatusOf(s);

    protected static async Task<FakeAuthService> SignedInAuthAsync()
    {
        var auth = new FakeAuthService { HasSavedSession = true };
        await auth.TryRestoreSessionAsync();
        return auth;
    }

}

public class LibraryFakeStatesTests : FakeStatesBase
{

    [Theory]
    [InlineData(Scenario.Data)]
    [InlineData(Scenario.Empty)]
    [InlineData(Scenario.Private)]
    [InlineData(Scenario.Error)]
    public async Task Library_ReturnsStateForScenario(Scenario scenario)
    {
        var fake = new FakeLibraryService { Status = StatusOf(scenario), ReturnEmpty = scenario == Scenario.Empty };

        var result = await fake.GetMyLibraryAsync(new LibraryFilter());

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        Assert.Equal(scenario == Scenario.Data, result.Value!.Count > 0);
    }

    [Fact]
    public async Task Library_OnlyNeverPlayed_ReturnsOnlyZeroPlaytime()
    {
        var fake = new FakeLibraryService();

        var result = await fake.GetMyLibraryAsync(new LibraryFilter(OnlyNeverPlayed: true));

        Assert.All(result.Value!, g => Assert.Equal(0, g.PlaytimeForeverMinutes));
    }

}

public class AchievementsFakeStatesTests : FakeStatesBase
{

    [Theory]
    [InlineData(Scenario.Data)]
    [InlineData(Scenario.Empty)]
    [InlineData(Scenario.Private)]
    [InlineData(Scenario.Error)]
    public async Task Achievements_GamesInProgress_ReturnsStateForScenario(Scenario scenario)
    {
        var fake = new FakeAchievementService { Status = StatusOf(scenario), ReturnEmpty = scenario == Scenario.Empty };

        var result = await fake.GetGamesInProgressAsync();

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        if (scenario is Scenario.Data or Scenario.Empty)
        {
            Assert.Equal(scenario == Scenario.Data, result.Value!.Count > 0);
        }
    }

    [Theory]
    [InlineData(Scenario.Private)]
    [InlineData(Scenario.Error)]
    public async Task Achievements_Plan_FailureStatusesPassThrough(Scenario scenario)
    {
        var fake = new FakeAchievementService { Status = StatusOf(scenario) };

        var result = await fake.GetPlanAsync(appId: 1);

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Achievements_Plan_UnknownGame_IsAvailableWithEmptyPlan()
    {
        var fake = new FakeAchievementService();

        var result = await fake.GetPlanAsync(appId: -1);

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!.Remaining);
    }

}

public class ComparisonFakeStatesTests : FakeStatesBase
{

    [Theory]
    [InlineData(Scenario.Data)]
    [InlineData(Scenario.Empty)]
    [InlineData(Scenario.Private)]
    [InlineData(Scenario.Error)]
    public async Task Comparison_ReturnsStateForScenario(Scenario scenario)
    {
        var library = new FakeLibraryService();
        var auth = await SignedInAuthAsync();
        var fake = new FakeComparisonService(library, auth)
        {
            Status = StatusOf(scenario),
            ReturnEmpty = scenario == Scenario.Empty
        };
        int appId = (await library.GetMyLibraryAsync(new LibraryFilter())).Value!.First().AppId;

        var result = await fake.GetComparisonAsync(appId);

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        if (scenario == Scenario.Data)
        {
            Assert.True(result.Value!.Ranking.Count > 1);
        }
        else if (scenario == Scenario.Empty)
        {
            // Друзів немає — у рейтингу лише сам користувач
            Assert.Single(result.Value!.Ranking);
            Assert.True(result.Value.Ranking[0].IsCurrentUser);
        }
    }

    [Fact]
    public async Task Comparison_GameNotInLibrary_IsError()
    {
        var fake = new FakeComparisonService(new FakeLibraryService(), await SignedInAuthAsync());

        var result = await fake.GetComparisonAsync(appId: -1);

        Assert.Equal(DataStatus.Error, result.Status);
    }

}

public class FriendsRadarFakeStatesTests : FakeStatesBase
{

    [Theory]
    [InlineData(Scenario.Data)]
    [InlineData(Scenario.Empty)]
    [InlineData(Scenario.Private)]
    [InlineData(Scenario.Error)]
    public async Task FriendsRadar_ReturnsStateForScenario(Scenario scenario)
    {
        var fake = new FakeFriendsRadarService { Status = StatusOf(scenario), ReturnEmpty = scenario == Scenario.Empty };

        var result = await fake.GetFriendsAsync(new RadarFilter());

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        Assert.Equal(scenario == Scenario.Data, result.Value!.Count > 0);
    }

    [Fact]
    public async Task FriendsRadar_OnlyAvailable_IsSubsetOfAllWithoutOfflineOrInGame()
    {
        var fake = new FakeFriendsRadarService();

        var all = (await fake.GetFriendsAsync(new RadarFilter())).Value!;
        var available = (await fake.GetFriendsAsync(new RadarFilter(OnlyAvailable: true))).Value!;

        Assert.NotEmpty(available);
        Assert.True(available.Count < all.Count);
        Assert.All(available, f =>
        {
            Assert.Contains(f.SteamId, all.Select(a => a.SteamId));
            Assert.Null(f.CurrentGameId);
            Assert.True(f.PersonaState is PersonaState.Online or PersonaState.LookingToPlay);
        });
    }

}

public class MatchmakerFakeStatesTests : FakeStatesBase
{

    [Theory]
    [InlineData(Scenario.Data)]
    [InlineData(Scenario.Empty)]
    [InlineData(Scenario.Error)]
    public async Task Matchmaker_ReturnsStateForScenario(Scenario scenario)
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync())
        {
            Status = StatusOf(scenario),
            ReturnEmpty = scenario == Scenario.Empty
        };

        var result = await fake.FindCommonGamesAsync(new[] { "Андрій", "Марта" });

        Assert.Equal(ExpectedStatus(scenario), result.Status);
        if (scenario == Scenario.Data)
        {
            Assert.NotEmpty(result.Value!.CommonGames);
        }
        else if (scenario == Scenario.Empty)
        {
            Assert.Empty(result.Value!.CommonGames);
        }
    }

    [Fact]
    public async Task Matchmaker_HiddenLibrary_StaysOutOfTheIntersectionButIsReported()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = (await fake.FindCommonGamesAsync(new[] { "Андрій", "Марта", "Олег" })).Value!;

        Assert.Equal(4, result.Participants.Count);
        Assert.True(result.Participants[0].IsCurrentUser);
        Assert.Equal("Олег", Assert.Single(result.HiddenLibraryParticipants).PersonaName);
        Assert.Equal(6, result.CommonGames.Count);
        // Від найбільшого середнього часу до найменшого
        Assert.Equal(
            result.CommonGames.OrderByDescending(g => g.AveragePlaytimeMinutes).Select(g => g.AppId),
            result.CommonGames.Select(g => g.AppId));
        // Олега в часі гри немає: він не бере участі в перетині
        Assert.All(result.CommonGames, g => Assert.Equal(3, g.PlaytimeMinutesBySteamId.Count));
    }

    [Fact]
    public async Task Matchmaker_OnlyHiddenLibraryBesidesUser_NothingToCompare()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = await fake.FindCommonGamesAsync(new[] { "Олег" });

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Empty(result.Value!.CommonGames);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Matchmaker_InvalidInputsAreReportedNotThrown()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = (await fake.FindCommonGamesAsync(new[] { "Андрій", "Невідомий гравець", "76561198999999999" })).Value!;

        Assert.Equal(2, result.InvalidInputs.Count);
        Assert.Equal(2, result.Participants.Count);
    }

    [Fact]
    public async Task Matchmaker_DuplicatesBlankAndSelfAreIgnored()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = (await fake.FindCommonGamesAsync(new[] { "Андрій", "андрій", "  ", "", "76561198000000001" })).Value!;

        Assert.Equal(2, result.Participants.Count);
        Assert.Empty(result.InvalidInputs);
    }

    [Fact]
    public async Task Matchmaker_OnlyInvalidInputs_IsErrorWithReason()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = await fake.FindCommonGamesAsync(new[] { "Невідомий гравець" });

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.Contains("Немає жодного іншого учасника", result.Message);
    }

    [Fact]
    public async Task Matchmaker_NoOneSelected_IsError()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());

        var result = await fake.FindCommonGamesAsync(Array.Empty<string>());

        Assert.Equal(DataStatus.Error, result.Status);
    }

    [Fact]
    public async Task Matchmaker_MoreThanMaxParticipants_IsError()
    {
        var fake = new FakeMatchmakerService(await SignedInAuthAsync());
        // 8 друзів + користувач = 9 учасників при максимумі 8
        var inputs = new[] { "Андрій", "Марта", "Денис", "Олег", "Софія", "Тарас", "Ірина", "Віка" };

        var result = await fake.FindCommonGamesAsync(inputs);

        Assert.Equal(DataStatus.Error, result.Status);
        Assert.Contains("Забагато", result.Message);
    }

    [Fact]
    public async Task Matchmaker_NotSignedIn_IsError()
    {
        var fake = new FakeMatchmakerService(new FakeAuthService());

        var result = await fake.FindCommonGamesAsync(new[] { "Андрій" });

        Assert.Equal(DataStatus.Error, result.Status);
    }
}
