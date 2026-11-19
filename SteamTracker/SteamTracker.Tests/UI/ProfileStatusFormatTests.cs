using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.Profile;

namespace SteamTracker.Tests.UI;

public class ProfileStatusFormatTests
{
    private static PlayerPresence Me(PersonaState state, int? gameId = null, string? gameName = null) =>
        new("76561198000000001", "Я", "", ProfileVisibility.Public, state, gameId, gameName);

    [Fact]
    public void Of_InGame_ShowsGameName()
    {
        var (text, brush) = ProfileStatusFormat.Of(Me(PersonaState.Online, 570, "Dota 2"), ProfileVisibility.Public);

        Assert.Equal("У грі: Dota 2", text);
        Assert.Equal("Success", brush);
    }

    [Fact]
    public void Of_NonSteamGame_ShowsGameNameWithoutId()
    {
        var (text, _) = ProfileStatusFormat.Of(Me(PersonaState.Online, null, "Non-Steam Game"), ProfileVisibility.Public);

        Assert.Equal("У грі: Non-Steam Game", text);
    }

    [Fact]
    public void Of_InGameWithoutName_ShowsJustInGame()
    {
        var (text, brush) = ProfileStatusFormat.Of(Me(PersonaState.Online, 570, null), ProfileVisibility.Public);

        Assert.Equal("У грі", text);
        Assert.Equal("Success", brush);
    }

    [Theory]
    [InlineData(PersonaState.Online, "Онлайн", "Accent")]
    [InlineData(PersonaState.LookingToPlay, "Онлайн", "Accent")]
    [InlineData(PersonaState.Away, "Немає на місці", "Warning")]
    [InlineData(PersonaState.Busy, "Не турбувати", "Warning")]
    [InlineData(PersonaState.Offline, "Офлайн", "TextSecondary")]
    public void Of_NotInGame_ByPersonaState(PersonaState state, string expectedText, string expectedBrush)
    {
        var (text, brush) = ProfileStatusFormat.Of(Me(state), ProfileVisibility.Public);

        Assert.Equal(expectedText, text);
        Assert.Equal(expectedBrush, brush);
    }

    [Theory]
    [InlineData(ProfileVisibility.Public, "Статус недоступний", "TextSecondary")]
    [InlineData(ProfileVisibility.Limited, "Статус недоступний", "TextSecondary")]
    [InlineData(ProfileVisibility.Private, "Приватний профіль", "Danger")]
    public void Of_NoPresence_FallsBackByVisibility(ProfileVisibility visibility, string expectedText, string expectedBrush)
    {
        var (text, brush) = ProfileStatusFormat.Of(null, visibility);

        Assert.Equal(expectedText, text);
        Assert.Equal(expectedBrush, brush);
    }
}
