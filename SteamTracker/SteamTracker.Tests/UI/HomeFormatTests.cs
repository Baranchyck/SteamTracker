using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.Home;

namespace SteamTracker.Tests.UI;

public class HomeFormatTests
{
    [Theory]
    [InlineData(2020, 6, 15, 2026, 6, 14, 5)]   // день народження акаунту ще не настав
    [InlineData(2020, 6, 15, 2026, 6, 15, 6)]   // рівно в день
    [InlineData(2020, 6, 15, 2020, 6, 15, 0)]   // зареєструвався сьогодні
    [InlineData(2020, 6, 15, 2020, 12, 31, 0)]
    [InlineData(2020, 2, 29, 2021, 2, 28, 1)]   // 29 лютого: у невисокосному році річниця — 28-го
    [InlineData(2026, 12, 31, 2026, 1, 1, 0)]   // «створено в майбутньому» (збій годинника) — не мінус
    public void FullYears_CountsOnlyCompletedYears(int cy, int cm, int cd, int ny, int nm, int nd, int expected) =>
        Assert.Equal(expected, HomeFormat.FullYears(UkText.LocalNoon(cy, cm, cd), UkText.LocalNoon(ny, nm, nd)));

    [Theory]
    [InlineData(0, AccountBadge.Newbie)]
    [InlineData(1, AccountBadge.None)]
    [InlineData(4, AccountBadge.None)]
    [InlineData(5, AccountBadge.Veteran)]
    [InlineData(20, AccountBadge.Veteran)]
    public void Badge_ByAccountAge(int years, AccountBadge expected) =>
        Assert.Equal(expected, HomeFormat.Badge(years));

    [Theory]
    [InlineData(0, "Перший рік")]
    [InlineData(1, "1 рік")]
    [InlineData(2, "2 роки")]
    [InlineData(4, "4 роки")]
    [InlineData(5, "5 років")]
    [InlineData(11, "11 років")]
    [InlineData(12, "12 років")]
    [InlineData(21, "21 рік")]
    [InlineData(22, "22 роки")]
    public void Years_PicksCorrectPlural(int years, string expected) =>
        Assert.Equal(expected, HomeFormat.Years(years));

    [Fact]
    public void Since_UsesGenitiveMonth() =>
        Assert.Equal("з 7 березня 2020", HomeFormat.Since(UkText.LocalNoon(2020, 3, 7)));

    // ---------- редизайн за макетом (#145) ----------

    private static OwnedGame Game(string name, int forever, int twoWeeks = 0) =>
        new(1, name, forever, twoWeeks, null, "");

    private static PlayerPresence Friend(PersonaState state, string? gameName = null) =>
        new("76561198000000001", "Друг", "", ProfileVisibility.Public, state, null, gameName);

    [Theory]
    [InlineData("yulian_k", "Привіт, yulian_k!")]
    [InlineData("  yulian_k ", "Привіт, yulian_k!")]
    [InlineData("", "Привіт!")]
    [InlineData(null, "Привіт!")]
    public void Greeting_WithOrWithoutName(string? name, string expected) =>
        Assert.Equal(expected, HomeFormat.Greeting(name));

    [Fact]
    public void BarelyPlayedCount_LessThanHour()
    {
        var games = new[] { Game("A", 0), Game("B", 59), Game("C", 60), Game("D", 600) };
        Assert.Equal(2, HomeFormat.BarelyPlayedCount(games));
    }

    [Theory]
    [InlineData(97, "з них 97 майже без запуску")]
    [InlineData(0, "у кожну вже грав понад годину")]
    public void BarelyPlayedCaption(int count, string expected) =>
        Assert.Equal(expected, HomeFormat.BarelyPlayedCaption(count));

    [Theory]
    [InlineData(97, "97 ігор чекають на запуск")]
    [InlineData(1, "1 гра чекає на запуск")]
    [InlineData(3, "3 гри чекають на запуск")]
    [InlineData(11, "11 ігор чекають на запуск")]
    [InlineData(21, "21 гра чекає на запуск")]
    [InlineData(0, "Усі ігри вже запускались")]
    public void WaitingForLaunch_PicksCorrectPlural(int count, string expected) =>
        Assert.Equal(expected, HomeFormat.WaitingForLaunch(count));

    [Fact]
    public void RecentlyPlayed_OnlyLastTwoWeeks_MostActiveFirst_Limited()
    {
        var games = new[]
        {
            Game("Stardew Valley", 4260, 60),
            Game("Dota 2", 52380, 0),
            Game("Starfall Tactics", 7560, 360),
            Game("Counter-Strike 2", 36600, 240),
            Game("Hades", 600, 30)
        };

        var recent = HomeFormat.RecentlyPlayed(games, 3);

        Assert.Equal(new[] { "Starfall Tactics", "Counter-Strike 2", "Stardew Valley" }, recent.Select(g => g.Name));
    }

    [Fact]
    public void RecentlyPlayed_EqualTime_SortedByName()
    {
        var games = new[] { Game("Portal 2", 100, 120), Game("Hades", 100, 120) };
        Assert.Equal(new[] { "Hades", "Portal 2" }, HomeFormat.RecentlyPlayed(games, 5).Select(g => g.Name));
    }

    [Theory]
    [InlineData(360, "+6 год за 2 тижні")]
    [InlineData(40, "+40 хв за 2 тижні")]
    public void RecentPlaytime_HoursOrMinutes(int minutes, string expected) =>
        Assert.Equal(expected, HomeFormat.RecentPlaytime(minutes));

    [Fact]
    public void TotalPlaytime_WholeHoursWithThousands() =>
        Assert.Equal("Усього 2 847 год", UkText.Norm(HomeFormat.TotalPlaytime(2847 * 60)));

    [Theory]
    [InlineData(PersonaState.Online, null, true)]
    [InlineData(PersonaState.Away, null, true)]
    [InlineData(PersonaState.Offline, null, false)]
    [InlineData(PersonaState.Offline, "Deep Rock Galactic", true)] // у грі з невидимим статусом — все одно у Steam
    public void IsInSteam_EveryoneExceptOffline(PersonaState state, string? game, bool expected) =>
        Assert.Equal(expected, HomeFormat.IsInSteam(Friend(state, game)));

    [Fact]
    public void FriendsStats_AsInDesign()
    {
        Assert.Equal("6 з 12", HomeFormat.OfTotal(6, 12));
        Assert.Equal("3 у грі · 3 онлайн", HomeFormat.FriendsBreakdown(inSteam: 6, inGame: 3));
    }

    [Theory]
    [InlineData(PersonaState.Online, "Starfall Tactics", "У грі · Starfall Tactics")]
    [InlineData(PersonaState.Online, null, "Онлайн")]
    [InlineData(PersonaState.Away, null, "Немає на місці")]
    public void StatusLine_AddsGameName(PersonaState state, string? game, string expected) =>
        Assert.Equal(expected, HomeFormat.StatusLine(Friend(state, game)));
}
