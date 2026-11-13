using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.Profile;

public class ProfileStatusService : IProfileStatusService
{
    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;

    public ProfileStatusService(ISteamApiClient steamApi, IAuthService auth)
    {
        _steamApi = steamApi;
        _auth = auth;
    }

    public async Task<PlayerPresence?> GetCurrentStatusAsync(CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null)
            return null;

        try
        {
            // GetPlayerSummaries віддає personastate і поточну гру; GetPlayerSummary (SteamProfile) — ні
            var summaries = await _steamApi.GetPlayerSummariesAsync(new[] { user.SteamId }, ct);
            return summaries.FirstOrDefault(p => p.SteamId == user.SteamId);
        }
        catch (SteamApiException)
        {
            // Статус — не критична інформація: картка покаже запасний текст, а наступне оновлення спробує знову
            return null;
        }
    }
}
