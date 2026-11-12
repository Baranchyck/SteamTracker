using SteamTracker.UI.Views.Achievements;

namespace SteamTracker.Tests.UI;

public class AchievementFormatTests
{
    [Theory]
    [InlineData(0, "0%")]
    [InlineData(0.5, "0%")]       // прогрес не округлюється вгору: 99,99% ще не 100%
    [InlineData(69.39, "69%")]
    [InlineData(99.99, "99%")]
    [InlineData(100, "100%")]
    public void ProgressPercent_RoundsDown(double percent, string expected) =>
        Assert.Equal(expected, AchievementFormat.ProgressPercent((decimal)percent));

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(79.59, "79,6%")]
    [InlineData(80, "80%")]       // без зайвого «,0»
    [InlineData(100, "100%")]
    public void Percent_OneOptionalDecimal(double percent, string expected) =>
        Assert.Equal(expected, AchievementFormat.Percent((decimal)percent));

    [Theory]
    [InlineData(null, "немає даних")]
    [InlineData(0.0, "0,0% гравців")]
    [InlineData(78.4, "78,4% гравців")]
    [InlineData(100.0, "100,0% гравців")]
    public void GlobalPercent_NullMeansNoData(double? percent, string expected) =>
        Assert.Equal(expected, AchievementFormat.GlobalPercent(percent));

    [Fact]
    public void Closed_ComposesSentence() =>
        Assert.Equal("3 з 10 досягнень закрито", AchievementFormat.Closed(3, 10));

    [Theory]
    [InlineData(0, "0 досягнень")]
    [InlineData(1, "1 досягнення")]
    [InlineData(2, "2 досягнення")]
    [InlineData(4, "4 досягнення")]
    [InlineData(5, "5 досягнень")]
    [InlineData(11, "11 досягнень")]
    [InlineData(12, "12 досягнень")]
    [InlineData(14, "14 досягнень")]
    [InlineData(21, "21 досягнення")]
    [InlineData(25, "25 досягнень")]
    [InlineData(101, "101 досягнення")]
    [InlineData(111, "111 досягнень")]
    public void Achievements_PicksCorrectPlural(int count, string expected) =>
        Assert.Equal(expected, AchievementFormat.Achievements(count));
}
