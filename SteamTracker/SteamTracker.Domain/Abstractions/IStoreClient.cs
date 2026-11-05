using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Неофіційний Store API: інший хост і без SLA, тому окремо від ISteamApiClient.
public interface IStoreClient
{
    // Не кидає винятків (крім скасування через ct): при збої повертає те, що встигло завантажитись.
    // AppId, якого немає в результаті, — ціну отримати не вдалося.
    Task<IReadOnlyList<GamePrice>> GetPricesAsync(IEnumerable<int> appIds, CancellationToken ct = default);
}
