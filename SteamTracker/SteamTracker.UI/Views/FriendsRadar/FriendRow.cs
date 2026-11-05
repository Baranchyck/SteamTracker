using System.Windows.Media;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Views.FriendsRadar
{
    /// <summary>Група статусу для кольору й легенди: «У грі», «Онлайн», «Відійшли», «Офлайн».</summary>
    public enum FriendGroup { InGame, Online, Away, Offline }

    /// <summary>Картка друга: вже підготовлені значення для показу.</summary>
    /// <param name="AvatarUrl">Порожній — лишається коло з першою літерою ніка.</param>
    /// <param name="GameName">Назва поточної гри; null — рядок з грою не показується.</param>
    public record FriendRow(
        string Name,
        string Initial,
        string AvatarUrl,
        Brush AvatarBrush,
        string StatusText,
        Brush StatusBrush,
        string? GameName,
        double Opacity)
    {
        public bool HasGame => GameName is not null;
    }

    internal static class FriendStatus
    {
        // Кольори заглушок аватарів, як у макеті; колір обирається за SteamId, тож у друга він не змінюється
        private static readonly Brush[] AvatarPalette =
        {
            Freeze("#E07B53"), Freeze("#7FB77E"), Freeze("#E6C17A"), Freeze("#9B88C9"),
            Freeze("#4CC9D9"), Freeze("#E57373"), Freeze("#7FA99B"), Freeze("#8E6FA8"),
            Freeze("#6C8EA8"), Freeze("#B08D57"), Freeze("#7E8C69"), Freeze("#A58AA5")
        };

        // Як у FriendsRadarService: «у грі» перевіряється першим; для сторонніх (non-Steam) ігор
        // CurrentGameId == null, але назва є — це теж «у грі»
        public static bool IsInGame(PlayerPresence friend) =>
            friend.CurrentGameId is not null || !string.IsNullOrEmpty(friend.CurrentGameName);

        public static FriendGroup GroupOf(PlayerPresence friend) =>
            IsInGame(friend) ? FriendGroup.InGame : friend.PersonaState switch
            {
                PersonaState.Online or PersonaState.LookingToPlay or PersonaState.LookingToTrade => FriendGroup.Online,
                PersonaState.Away or PersonaState.Busy or PersonaState.Snooze => FriendGroup.Away,
                _ => FriendGroup.Offline
            };

        // Те саме правило, що в RadarFilter.OnlyAvailable: онлайн або «шукає гру» і зараз не в грі.
        // Тут лише для лічильника «Вільні зараз · N»; сам список фільтрує сервіс
        public static bool IsAvailable(PlayerPresence friend) =>
            friend.PersonaState is PersonaState.Online or PersonaState.LookingToPlay && !IsInGame(friend);

        public static string Text(PlayerPresence friend) => GroupOf(friend) switch
        {
            FriendGroup.InGame => "У грі",
            FriendGroup.Online => "Онлайн",
            FriendGroup.Offline => "Офлайн",
            _ => friend.PersonaState == PersonaState.Busy ? "Не турбувати" : "Немає на місці"
        };

        /// <summary>Ключ кольору з Colors.xaml для групи статусу.</summary>
        public static string BrushKey(FriendGroup group) => group switch
        {
            FriendGroup.InGame => "Success",
            FriendGroup.Online => "Accent",
            FriendGroup.Away => "Warning",
            _ => "TextSecondary"
        };

        public static Brush AvatarBrush(string steamId) =>
            AvatarPalette[(int)((ulong.TryParse(steamId, out var id) ? id : (ulong)steamId.Length) % (ulong)AvatarPalette.Length)];

        public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

        private static Brush Freeze(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }
}
