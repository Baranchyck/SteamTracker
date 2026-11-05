using System.Globalization;

namespace SteamTracker.UI.Views.Achievements
{
    /// <summary>
    /// Як показувати відсотки й кількість досягнень на екрані «Досягнення» (українською).
    /// </summary>
    internal static class AchievementFormat
    {
        // uk-UA: кома в дробовій частині — «78,4%»
        private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

        /// <summary>Великий прогрес у картці: 69,39 → «69%».</summary>
        public static string ProgressPercent(decimal percent) => Math.Floor(percent).ToString("N0", Uk) + "%";

        /// <summary>Прогрес після roadmap: 79,59 → «79,6%».</summary>
        public static string Percent(decimal percent) => percent.ToString("0.#", Uk) + "%";

        /// <summary>Глобальний % ачівки: «78,4% гравців»; null — Steam не віддав статистику.</summary>
        public static string GlobalPercent(double? percent) =>
            percent is { } value ? value.ToString("0.0", Uk) + "% гравців" : "немає даних";

        public static string Closed(int achieved, int total) => $"{achieved} з {total} досягнень закрито";

        /// <summary>«1 досягнення», «3 досягнення», «5 досягнень», «11 досягнень», «21 досягнення».</summary>
        public static string Achievements(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;

            string word = last is >= 1 and <= 4 && lastTwo is < 12 or > 14 ? "досягнення" : "досягнень";
            return $"{count} {word}";
        }
    }
}
