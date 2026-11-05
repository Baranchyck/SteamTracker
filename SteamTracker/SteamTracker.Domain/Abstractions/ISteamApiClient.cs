using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

public interface ISteamApiClient
{
    Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default);
    Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(string steamId, CancellationToken ct = default);

    // SteamID64 друзів. null — список друзів закритий налаштуваннями приватності.
    Task<IReadOnlyList<string>?> GetFriendIdsAsync(string steamId, CancellationToken ct = default);

    // Профілі кількох гравців; запити йдуть пачками по 100 SteamID (ліміт Steam).
    // Неіснуючі SteamID просто відсутні в результаті; порядок не гарантується.
    Task<IReadOnlyList<PlayerPresence>> GetPlayerSummariesAsync(
        IEnumerable<string> steamIds, CancellationToken ct = default);
}
