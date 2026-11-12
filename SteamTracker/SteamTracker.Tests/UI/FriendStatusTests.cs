using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.FriendsRadar;

namespace SteamTracker.Tests.UI;

public class FriendStatusTests
{
    private static PlayerPresence Friend(PersonaState state, int? gameId = null, string? gameName = null) =>
        new("76561198000000001", "Друг", "", ProfileVisibility.Public, state, gameId, gameName);

    [Theory]
    [InlineData(570, null, true)]            // звичайна гра зі Steam
    [InlineData(null, "Non-Steam Game", true)] // стороння гра: id немає, назва є
    [InlineData(null, "", false)]
    [InlineData(null, null, false)]
    public void IsInGame_IdOrNameCounts(int? gameId, string? gameName, bool expected) =>
        Assert.Equal(expected, FriendStatus.IsInGame(Friend(PersonaState.Online, gameId, gameName)));

    [Theory]
    [InlineData(PersonaState.Online, FriendGroup.Online)]
    [InlineData(PersonaState.LookingToPlay, FriendGroup.Online)]
    [InlineData(PersonaState.LookingToTrade, FriendGroup.Online)]
    [InlineData(PersonaState.Away, FriendGroup.Away)]
    [InlineData(PersonaState.Busy, FriendGroup.Away)]
    [InlineData(PersonaState.Snooze, FriendGroup.Away)]
    [InlineData(PersonaState.Offline, FriendGroup.Offline)]
    public void GroupOf_ByPersonaState(PersonaState state, FriendGroup expected) =>
        Assert.Equal(expected, FriendStatus.GroupOf(Friend(state)));

    [Theory]
    [InlineData(PersonaState.Offline)]
    [InlineData(PersonaState.Away)]
    [InlineData(PersonaState.Online)]
    public void GroupOf_InGameWinsOverPersonaState(PersonaState state) =>
        Assert.Equal(FriendGroup.InGame, FriendStatus.GroupOf(Friend(state, 570, "Dota 2")));

    [Theory]
    [InlineData(PersonaState.Online, null, true)]
    [InlineData(PersonaState.LookingToPlay, null, true)]
    [InlineData(PersonaState.Online, 570, false)]      // в грі — не вільний
    [InlineData(PersonaState.Away, null, false)]
    [InlineData(PersonaState.Busy, null, false)]
    [InlineData(PersonaState.Offline, null, false)]
    public void IsAvailable_OnlineAndNotInGame(PersonaState state, int? gameId, bool expected) =>
        Assert.Equal(expected, FriendStatus.IsAvailable(Friend(state, gameId, gameId is null ? null : "Dota 2")));

    [Theory]
    [InlineData(PersonaState.Online, "Онлайн")]
    [InlineData(PersonaState.Away, "Немає на місці")]
    [InlineData(PersonaState.Snooze, "Немає на місці")]
    [InlineData(PersonaState.Busy, "Не турбувати")]
    [InlineData(PersonaState.Offline, "Офлайн")]
    public void Text_ByPersonaState(PersonaState state, string expected) =>
        Assert.Equal(expected, FriendStatus.Text(Friend(state)));

    [Fact]
    public void Text_InGame() =>
        Assert.Equal("У грі", FriendStatus.Text(Friend(PersonaState.Online, 570, "Dota 2")));

    [Theory]
    [InlineData(FriendGroup.InGame, "Success")]
    [InlineData(FriendGroup.Online, "Accent")]
    [InlineData(FriendGroup.Away, "Warning")]
    [InlineData(FriendGroup.Offline, "TextSecondary")]
    public void BrushKey_ByGroup(FriendGroup group, string expected) =>
        Assert.Equal(expected, FriendStatus.BrushKey(group));

    [Theory]
    [InlineData("", "?")]
    [InlineData("андрій", "А")]
    [InlineData("Marta", "M")]
    [InlineData("😀 Max", "😀")]      // емодзі — це два символи UTF-16, не можна різати посередині
    [InlineData("ǆ", "Ǆ")]
    public void Initial_FirstLetterUppercase(string name, string expected) =>
        Assert.Equal(expected, FriendStatus.Initial(name));

    [Theory]
    [InlineData("76561198000000001")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData("99999999999999999999999999")]   // не влазить у ulong
    public void AvatarBrush_AnyIdGivesStableBrush(string steamId)
    {
        var first = FriendStatus.AvatarBrush(steamId);

        Assert.NotNull(first);
        Assert.Same(first, FriendStatus.AvatarBrush(steamId));
    }
}
