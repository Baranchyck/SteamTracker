namespace SteamTracker.Tests.UI;

// uk-UA ставить між тисячами нерозривний пробіл (U+00A0 або U+202F залежно від ICU): у тестах порівнюємо зі звичайним
internal static class UkText
{
    public static string Norm(string text) => text.Replace(' ', ' ').Replace(' ', ' ');

    // Дата «о 12:00 за місцевим часом»: форматування бере ToLocalTime(), тож результат не залежить від пояса машини
    public static DateTimeOffset LocalNoon(int year, int month, int day) =>
        new(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local));
}
