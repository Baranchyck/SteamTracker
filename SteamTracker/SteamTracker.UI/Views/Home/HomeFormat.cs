using System.Globalization;

namespace SteamTracker.UI.Views.Home
{
    public enum AccountBadge { None, Newbie, Veteran }

    /// <summary>
    /// Вік акаунту для картки «У Steam» на головному екрані.
    /// </summary>
    internal static class HomeFormat
    {
        private const int NewbieBelowYears = 1;
        private const int VeteranFromYears = 5;

        private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

        /// <summary>Повні роки від реєстрації до сьогодні (день народження акаунту ще не настав — рік не рахується).</summary>
        public static int FullYears(DateTimeOffset created, DateTimeOffset now)
        {
            var from = created.ToLocalTime().Date;
            var to = now.ToLocalTime().Date;

            int years = to.Year - from.Year;
            if (to < from.AddYears(years))
            {
                years--;
            }

            return Math.Max(0, years);
        }

        /// <summary>Новачок — перший рік, ветеран — від 5 років, між ними без бейджа.</summary>
        public static AccountBadge Badge(int years) => years switch
        {
            < NewbieBelowYears => AccountBadge.Newbie,
            >= VeteranFromYears => AccountBadge.Veteran,
            _ => AccountBadge.None
        };

        /// <summary>«1 рік», «3 роки», «5 років», «11 років», «21 рік»; 0 — «Перший рік».</summary>
        public static string Years(int years)
        {
            if (years == 0)
            {
                return "Перший рік";
            }

            int lastTwo = years % 100;
            int last = years % 10;

            string word = (last, lastTwo) switch
            {
                (1, not 11) => "рік",
                (>= 2 and <= 4, < 12 or > 14) => "роки",
                _ => "років"
            };

            return $"{years} {word}";
        }

        public static string Since(DateTimeOffset created) =>
            "з " + created.ToLocalTime().ToString("d MMMM yyyy", Uk);
    }
}
