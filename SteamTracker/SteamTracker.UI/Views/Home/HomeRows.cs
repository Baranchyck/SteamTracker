using System.Windows.Media;

namespace SteamTracker.UI.Views.Home
{
    /// <summary>Рядок «Нещодавно грав»: обкладинка, назва, загальний час і бейдж за 2 тижні.</summary>
    public record RecentGameRow(string Name, string IconUrl, string TotalText, string RecentText);

    /// <summary>Рядок «Друзі зараз»: аватар з точкою статусу, нік і статус (з грою, якщо грає).</summary>
    /// <param name="AvatarUrl">Порожній — лишається коло з першою літерою ніка.</param>
    public record FriendNowRow(
        string Name,
        string Initial,
        string AvatarUrl,
        Brush AvatarBrush,
        string StatusLine,
        Brush StatusBrush);
}
