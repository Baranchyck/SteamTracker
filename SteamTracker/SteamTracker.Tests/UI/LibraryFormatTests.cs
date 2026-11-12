using SteamTracker.UI.Views.Library;

namespace SteamTracker.Tests.UI;

public class LibraryFormatTests
{
    [Theory]
    [InlineData(0, "0,0 год")]
    [InlineData(1, "0,0 год")]
    [InlineData(59, "1,0 год")]         // 0,98 → одна цифра після коми
    [InlineData(60, "1,0 год")]         // рівно година
    [InlineData(61, "1,0 год")]
    [InlineData(90, "1,5 год")]
    [InlineData(4260, "71,0 год")]
    [InlineData(int.MaxValue, "35 791 394,1 год")]
    public void Hours_FormatsMinutesAsHoursWithOneDecimal(int minutes, string expected) =>
        Assert.Equal(expected, UkText.Norm(LibraryFormat.Hours(minutes)));

    [Theory]
    [InlineData(0, "0 год")]
    [InlineData(59, "1 год")]
    [InlineData(60, "1 год")]
    [InlineData(2847 * 60, "2 847 год")]
    [InlineData(10_000_000_000L, "166 666 667 год")]   // більше за int
    public void TotalHours_RoundsToWholeHours(long minutes, string expected) =>
        Assert.Equal(expected, UkText.Norm(LibraryFormat.TotalHours(minutes)));

    [Theory]
    [InlineData(0, "0 хв")]
    [InlineData(59, "59 хв")]
    [InlineData(1234, "1 234 хв")]
    public void Minutes_UsesThousandsSeparator(int minutes, string expected) =>
        Assert.Equal(expected, UkText.Norm(LibraryFormat.Minutes(minutes)));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1204, "1 204")]
    public void Count_UsesThousandsSeparator(int count, string expected) =>
        Assert.Equal(expected, UkText.Norm(LibraryFormat.Count(count)));

    [Fact]
    public void LastPlayed_NullDate_SaysNeverLaunched() =>
        Assert.Equal("Ще не запускалась", LibraryFormat.LastPlayed(null));

    [Theory]
    [InlineData(2026, 11, 5, "Востаннє: 5 листопада 2026")]
    [InlineData(2026, 1, 1, "Востаннє: 1 січня 2026")]
    [InlineData(2024, 2, 29, "Востаннє: 29 лютого 2024")]
    public void LastPlayed_UsesGenitiveMonth(int year, int month, int day, string expected) =>
        Assert.Equal(expected, LibraryFormat.LastPlayed(UkText.LocalNoon(year, month, day)));

    [Theory]
    [InlineData(0, "0 ігор")]
    [InlineData(1, "1 гра")]
    [InlineData(2, "2 гри")]
    [InlineData(4, "4 гри")]
    [InlineData(5, "5 ігор")]
    [InlineData(11, "11 ігор")]
    [InlineData(12, "12 ігор")]
    [InlineData(14, "14 ігор")]
    [InlineData(21, "21 гра")]
    [InlineData(22, "22 гри")]
    [InlineData(25, "25 ігор")]
    [InlineData(101, "101 гра")]
    [InlineData(111, "111 ігор")]
    [InlineData(1204, "1 204 гри")]
    public void Games_PicksCorrectPlural(int count, string expected) =>
        Assert.Equal(expected, UkText.Norm(LibraryFormat.Games(count)));
}
