using System.Globalization;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.Comparison;
using SteamTracker.UI.Views.FriendsRadar;
using SteamTracker.UI.Views.Library;

namespace SteamTracker.UI.Views.Home;

public enum AccountBadge { None, Newbie, Veteran }

/// <summary>
/// Тексти й відбір даних для головного екрана (макет «02 · Головна»).
/// Вік акаунту — з картки «У Steam» (#89), решта — з редизайну за макетом (#145).
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

        var years = to.Year - from.Year;
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

        var lastTwo = years % 100;
        var last = years % 10;

        var word = (last, lastTwo) switch
        {
            (1, not 11) => "рік",
            ( >= 2 and <= 4, < 12 or > 14) => "роки",
            _ => "років"
        };

        return $"{years} {word}";
    }

    public static string Since(DateTimeOffset created) =>
        "з " + created.ToLocalTime().ToString("d MMMM yyyy", Uk);

    // ---------- шапка ----------

    /// <summary>«Привіт, yulian_k!»; без ніка — просто «Привіт!».</summary>
    public static string Greeting(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Привіт!" : $"Привіт, {name.Trim()}!";

    // ---------- бібліотека ----------

    /// <summary>Ігри, у які майже не грали: менше години, як у списку «Майже не грав» на екрані бібліотеки.</summary>
    public static int BarelyPlayedCount(IEnumerable<OwnedGame> games) =>
        games.Count(g => g.PlaytimeForeverMinutes < LibraryFormat.BarelyPlayedMinutes);

    /// <summary>Підпис під кількістю ігор: «з них 97 майже без запуску».</summary>
    public static string BarelyPlayedCaption(int count) => count == 0
        ? "у кожну вже грав понад годину"
        : $"з них {LibraryFormat.Count(count)} майже без запуску";

    /// <summary>Опис картки модуля «Бібліотека»: «97 ігор чекають на запуск», «1 гра чекає на запуск».</summary>
    public static string WaitingForLaunch(int count)
    {
        if (count == 0)
        {
            return "Усі ігри вже запускались";
        }

        var verb = count % 10 == 1 && count % 100 != 11 ? "чекає" : "чекають";
        return $"{LibraryFormat.Games(count)} {verb} на запуск";
    }

    /// <summary>
    /// «Нещодавно грав»: ігри з активністю за 2 тижні, більше часу — вище; за рівного часу — за назвою.
    /// </summary>
    public static IReadOnlyList<OwnedGame> RecentlyPlayed(IEnumerable<OwnedGame> games, int max) =>
        games
            .Where(g => g.Playtime2WeeksMinutes > 0)
            .OrderByDescending(g => g.Playtime2WeeksMinutes)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(max)
            .ToList();

    /// <summary>Бейдж «+6 год за 2 тижні»; менше години — у хвилинах, як на екрані порівняння.</summary>
    public static string RecentPlaytime(int minutes) => $"+{ComparisonFormat.Hours(minutes)} за 2 тижні";

    /// <summary>Під назвою гри: «Усього 126 год».</summary>
    public static string TotalPlaytime(int minutes) => "Усього " + LibraryFormat.TotalHours(minutes);

    // ---------- друзі ----------

    /// <summary>Друзі, які зараз у Steam: усі, крім офлайн (у грі, онлайн, відійшли).</summary>
    public static bool IsInSteam(PlayerPresence friend) => FriendStatus.GroupOf(friend) != FriendGroup.Offline;

    /// <summary>«6 з 12».</summary>
    public static string OfTotal(int part, int total) => $"{LibraryFormat.Count(part)} з {LibraryFormat.Count(total)}";

    /// <summary>«3 у грі · 3 онлайн»: «онлайн» — ті, хто в Steam, але не в грі.</summary>
    public static string FriendsBreakdown(int inSteam, int inGame) =>
        $"{LibraryFormat.Count(inGame)} у грі · {LibraryFormat.Count(inSteam - inGame)} онлайн";

    /// <summary>Рядок статусу у списку «Друзі зараз»: «У грі · Starfall Tactics» або «Онлайн».</summary>
    public static string StatusLine(PlayerPresence friend)
    {
        var status = FriendStatus.Text(friend);
        return FriendStatus.IsInGame(friend) && !string.IsNullOrEmpty(friend.CurrentGameName)
            ? $"{status} · {friend.CurrentGameName}"
            : status;
    }
}
