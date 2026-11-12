using SteamTracker.UI.Views.Comparison;

namespace SteamTracker.Tests.UI;

public class ComparisonFormatTests
{
    [Theory]
    [InlineData(0, "0 год")]
    [InlineData(1, "1 хв")]
    [InlineData(30, "30 хв")]
    [InlineData(59, "59 хв")]
    [InlineData(59.5, "1 год")]      // не «60 хв»: після округлення це вже година
    [InlineData(60, "1 год")]
    [InlineData(120, "2 год")]
    [InlineData(12720, "212 год")]
    [InlineData(600_000, "10 000 год")]
    public void Hours_MinutesUnderAnHourStayMinutes(double minutes, string expected) =>
        Assert.Equal(expected, UkText.Norm(ComparisonFormat.Hours(minutes)));

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(0.4, "0%")]
    [InlineData(50, "50%")]
    [InlineData(1500, "1 500%")]
    public void Percent_WholeNumber(double percent, string expected) =>
        Assert.Equal(expected, UkText.Norm(ComparisonFormat.Percent((decimal)percent)));

    [Theory]
    [InlineData(0, "0 гравців")]
    [InlineData(1, "1 гравця")]
    [InlineData(2, "2 гравців")]
    [InlineData(7, "7 гравців")]
    [InlineData(11, "11 гравців")]
    [InlineData(21, "21 гравця")]
    public void Players_GenitiveAfterZ(int count, string expected) =>
        Assert.Equal(expected, ComparisonFormat.Players(count));

    [Theory]
    [InlineData(0, "0 друзів")]
    [InlineData(1, "1 друг")]
    [InlineData(2, "2 друзі")]
    [InlineData(4, "4 друзі")]
    [InlineData(5, "5 друзів")]
    [InlineData(11, "11 друзів")]
    [InlineData(21, "21 друг")]
    [InlineData(22, "22 друзі")]
    public void Friends_PicksCorrectPlural(int count, string expected) =>
        Assert.Equal(expected, ComparisonFormat.Friends(count));
}
