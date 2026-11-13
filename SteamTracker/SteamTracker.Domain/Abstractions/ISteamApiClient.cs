using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

public interface ISteamApiClient
{
    Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default);

    // null — бібліотека прихована налаштуваннями приватності; порожній список — ігор немає.
    Task<IReadOnlyList<OwnedGame>?> GetOwnedGamesAsync(string steamId, CancellationToken ct = default);

    // SteamID64 друзів. null — список друзів закритий налаштуваннями приватності.
    Task<IReadOnlyList<string>?> GetFriendIdsAsync(string steamId, CancellationToken ct = default);

    // Профілі кількох гравців; запити йдуть пачками по 100 SteamID (ліміт Steam).
    // Неіснуючі SteamID просто відсутні в результаті; порядок не гарантується.
    Task<IReadOnlyList<PlayerPresence>> GetPlayerSummariesAsync(
        IEnumerable<string> steamIds, CancellationToken ct = default);

    // Ачівки гравця в грі. null — профіль/статистика приватні; порожній список — у гри немає ачівок.
    Task<IReadOnlyList<PlayerAchievement>?> GetPlayerAchievementsAsync(
        string steamId, int appId, CancellationToken ct = default);

    // Глобальний % отримання кожної ачівки гри: ключ — ApiName, значення — 0..100.
    // Однаковий для всіх гравців, тож його можна кешувати на гру.
    Task<IReadOnlyDictionary<string, double>> GetGlobalAchievementPercentagesAsync(
        int appId, CancellationToken ct = default);

    // SteamID64 за vanity-ніком (steamcommunity.com/id/<vanity>). null — такого ніку немає.
    Task<string?> ResolveVanityUrlAsync(string vanity, CancellationToken ct = default);
}
