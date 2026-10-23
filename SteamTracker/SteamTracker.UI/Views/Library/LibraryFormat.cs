using System.Globalization;

namespace SteamTracker.UI.Views.Library
{
    /// <summary>
    /// Як показувати час і кількість ігор на екрані бібліотеки (українською).
    /// </summary>
    internal static class LibraryFormat
    {
        // uk-UA: «1 204,5» — пробіл між тисячами, кома в дробовій частині; місяці в родовому відмінку
        private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

        /// <summary>Години з одним знаком після коми: 4260 хв → «71,0 год».</summary>
        public static string Hours(int minutes) => (minutes / 60.0).ToString("N1", Uk) + " год";

        /// <summary>Підсумок цілими годинами, як у картці макета: «2 847 год».</summary>
        public static string TotalHours(long minutes) => (minutes / 60.0).ToString("N0", Uk) + " год";

        public static string Minutes(int minutes) => minutes.ToString("N0", Uk) + " хв";

        public static string Count(int count) => count.ToString("N0", Uk);

        public static string LastPlayed(DateTimeOffset? lastPlayed) => lastPlayed is { } date
            ? "Востаннє: " + date.ToLocalTime().ToString("d MMMM yyyy", Uk)
            : "Ще не запускалась";

        /// <summary>«1 гра», «3 гри», «5 ігор», «11 ігор», «21 гра».</summary>
        public static string Games(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;

            string word = (last, lastTwo) switch
            {
                (1, not 11) => "гра",
                (>= 2 and <= 4, < 12 or > 14) => "гри",
                _ => "ігор"
            };

            return $"{Count(count)} {word}";
        }
    }
}
