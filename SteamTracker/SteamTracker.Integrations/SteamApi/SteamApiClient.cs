using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Integrations.SteamApi;

public class SteamApiClient : ISteamApiClient
{
    private readonly HttpClient _httpClient;

    public SteamApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(string steamId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}