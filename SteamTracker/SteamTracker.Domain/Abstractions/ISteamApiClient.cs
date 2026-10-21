using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

public interface ISteamApiClient
{
    Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default);
    Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(string steamId, CancellationToken ct = default);
}