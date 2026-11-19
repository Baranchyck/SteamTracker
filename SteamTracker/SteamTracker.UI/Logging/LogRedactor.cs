using System.Text.RegularExpressions;

namespace SteamTracker.UI.Logging;

// Останній рубіж перед записом у файл: навіть якщо хтось залогує URL запиту або виняток,
// у тексті якого є ключ Steam API, у файл він не потрапить.
internal sealed class LogRedactor
{
    public const string Mask = "***";

    // Steam Web API приймає ключ у query: ...?key=XXXX&steamid=...
    private static readonly Regex KeyInQuery = new(
        @"([?&]key=)[^&\s""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IReadOnlyList<string> _secrets;

    public LogRedactor(IEnumerable<string?> secrets)
    {
        // Порожні й надто короткі значення не маскуємо: "***" замість кожної літери "a" зіпсує весь лог
        _secrets = secrets
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 8)
            .Select(s => s!)
            .Distinct()
            .ToList();
    }

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        foreach (var secret in _secrets)
            text = text.Replace(secret, Mask, StringComparison.Ordinal);

        return KeyInQuery.Replace(text, "$1" + Mask);
    }
}
