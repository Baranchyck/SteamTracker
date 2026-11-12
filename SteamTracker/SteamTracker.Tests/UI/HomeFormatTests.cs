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
}
