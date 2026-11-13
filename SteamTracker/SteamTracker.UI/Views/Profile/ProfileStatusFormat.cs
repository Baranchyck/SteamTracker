using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.FriendsRadar;

namespace SteamTracker.UI.Views.Profile
{
    /// <summary>Текст і колір статусу поточного користувача в картці бічного меню.</summary>
    internal static class ProfileStatusFormat
    {
        /// <param name="presence">null — статус отримати не вдалося.</param>
        /// <param name="visibility">Видимість профілю: запасний текст, коли статусу немає.</param>
        public static (string Text, string BrushKey) Of(PlayerPresence? presence, ProfileVisibility visibility)
        {
            if (presence is null)
            {
                return visibility == ProfileVisibility.Private
                    ? ("Приватний профіль", "Danger")
                    : ("Статус недоступний", "TextSecondary");
            }

            var group = FriendStatus.GroupOf(presence);
            var text = group == FriendGroup.InGame && !string.IsNullOrEmpty(presence.CurrentGameName)
                ? $"У грі: {presence.CurrentGameName}"
                : FriendStatus.Text(presence);

            return (text, FriendStatus.BrushKey(group));
        }
    }
}
