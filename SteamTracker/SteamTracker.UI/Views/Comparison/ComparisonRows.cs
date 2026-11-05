using System.Globalization;
using System.Windows.Media;

namespace SteamTracker.UI.Views.Comparison
{
    /// <summary>Рядок рейтингу: вже підготовлені значення для показу.</summary>
    /// <param name="BadgeText">«+14 год за 2 тижні»; null — бейдж не показується.</param>
    /// <param name="BarWidth">Довжина смужки: частка від найбільшого часу в рейтингу.</param>
    public record RankRow(
        string RankText,
        Brush RankBrush,
        string Initial,
        Brush AvatarBrush,
        string AvatarUrl,
        string Name,
        string? BadgeText,
        double BarWidth,
        Brush BarBrush,
        string HoursText,
        Brush RowBackground)
    {
        public bool HasBadge => BadgeText is not null;
    }

    /// <summary>Друг у бічних списках «Немає цієї гри» і «Бібліотека прихована».</summary>
    public record FriendItem(string Initial, Brush AvatarBrush, string AvatarUrl, string Name);

    /// <summary>Як показувати час і кількість людей на екрані порівняння (українською).</summary>
    internal static class ComparisonFormat
    {
        private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

        /// <summary>Цілі години, як у макеті («212 год»); менше години — хвилини («30 хв»).</summary>
        public static string Hours(double minutes) => minutes is > 0 and < 60
            ? $"{minutes.ToString("N0", Uk)} хв"
            : $"{(minutes / 60).ToString("N0", Uk)} год";

        public static string Percent(decimal percent) => percent.ToString("N0", Uk) + "%";

        /// <summary>Родовий відмінок після «з»: «з 1 гравця», «з 7 гравців», «з 21 гравця».</summary>
        public static string Players(int count) =>
            $"{count} {(count % 10 == 1 && count % 100 != 11 ? "гравця" : "гравців")}";

        /// <summary>«1 друг», «2 друзі», «5 друзів», «11 друзів», «22 друзі».</summary>
        public static string Friends(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;

            string word = (last, lastTwo) switch
            {
                (1, not 11) => "друг",
                (>= 2 and <= 4, < 12 or > 14) => "друзі",
                _ => "друзів"
            };

            return $"{count} {word}";
        }
    }
}
