using SteamTracker.UI.Views.Matchmaker;

namespace SteamTracker.Tests.UI;

public class MatchmakerFormatTests
{
    [Theory]
    [InlineData(1, "1 учасник")]
    [InlineData(2, "2 учасники")]
    [InlineData(4, "4 учасники")]
    [InlineData(5, "5 учасників")]
    [InlineData(8, "8 учасників")]
    [InlineData(11, "11 учасників")]
    [InlineData(21, "21 учасник")]
    public void Participants_PicksCorrectPlural(int count, string expected) =>
        Assert.Equal(expected, MatchmakerFormat.Participants(count));

    [Theory]
    [InlineData(2, "Знайти спільні ігри · 2 учасники")]
    [InlineData(8, "Знайти спільні ігри · 8 учасників")]
    public void FindButton_ShowsParticipantCount(int count, string expected) =>
        Assert.Equal(expected, MatchmakerFormat.FindButton(count));

    [Fact]
    public void HiddenLibraries_OnePlayer_SingularWithoutDecliningTheNickname() =>
        Assert.Equal(
            "Результат неповний: бібліотека гравця «Олег» прихована, тому її ігри не враховано.",
            MatchmakerFormat.HiddenLibraries(new[] { "Олег" }));

    [Fact]
    public void HiddenLibraries_SeveralPlayers_Plural() =>
        Assert.Equal(
            "Результат неповний: бібліотеки гравців «Олег», «Марта» приховані, тому їхні ігри не враховано.",
            MatchmakerFormat.HiddenLibraries(new[] { "Олег", "Марта" }));
}
