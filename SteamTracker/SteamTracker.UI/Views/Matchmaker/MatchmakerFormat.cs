namespace SteamTracker.UI.Views.Matchmaker
{
    /// <summary>Тексти екрана Матчмейкера українською.</summary>
    internal static class MatchmakerFormat
    {
        /// <summary>«2 учасники», «5 учасників», «21 учасник».</summary>
        public static string Participants(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;

            string word = (last, lastTwo) switch
            {
                (1, not 11) => "учасник",
                (>= 2 and <= 4, < 12 or > 14) => "учасники",
                _ => "учасників"
            };

            return $"{count} {word}";
        }

        /// <summary>Підпис на кнопці пошуку: «Знайти спільні ігри · 4 учасники».</summary>
        public static string FindButton(int participants) => $"Знайти спільні ігри · {Participants(participants)}";

        /// <summary>Попередження про приховані бібліотеки: ігри цих учасників у перетині не враховані.</summary>
        public static string HiddenLibraries(IReadOnlyList<string> names) => names.Count == 1
            ? $"Результат неповний: бібліотека гравця «{names[0]}» прихована, тому її ігри не враховано."
            : $"Результат неповний: бібліотеки гравців {string.Join(", ", names.Select(n => $"«{n}»"))} приховані, тому їхні ігри не враховано.";
    }
}
