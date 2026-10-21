using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ISteamOpenIdAuthenticator _authenticator;
    private readonly ISteamApiClient _steamApi;
    private readonly IUserRepository _users;
    private readonly ISettingsRepository _settings;
    
    public AuthService(
        ISteamOpenIdAuthenticator authenticator,
        ISteamApiClient steamApi,
        IUserRepository users,
        ISettingsRepository settings)
    {
        _authenticator = authenticator;
        _steamApi = steamApi;
        _users = users;
        _settings = settings;
    }

    public SteamProfile? CurrentUser { get; private set; }

    public event EventHandler? CurrentUserChanged;

    public async Task<SteamProfile> LoginAsync(CancellationToken ct = default)
    {
        var steamId = await _authenticator.AuthenticateAsync(ct);

        SteamProfile? profile;
        try
        {
            profile = await _steamApi.GetPlayerSummaryAsync(steamId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AuthFailedException("Не вдалося отримати профіль Steam.", ex);
        }

        if (profile is null)
            throw new AuthFailedException($"Steam повернув порожній профіль для {steamId}.");

        await _users.UpsertAsync(profile);
        await _settings.SetAsync(SettingsKeys.CurrentSteamId, profile.SteamId);
        SetCurrentUser(profile);
        return profile;
    }

    public async Task LogoutAsync()
    {
        await _settings.DeleteAsync(SettingsKeys.CurrentSteamId);
        SetCurrentUser(null);
    }

    public async Task<SteamProfile?> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        var steamId = await _settings.GetAsync(SettingsKeys.CurrentSteamId);
        if (string.IsNullOrEmpty(steamId))
            return null;

        var profile = await _users.GetAsync(steamId);
        if (profile is null)
            return null;

        SetCurrentUser(profile);
        return profile;
    }

    private void SetCurrentUser(SteamProfile? user)
    {
        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }
}