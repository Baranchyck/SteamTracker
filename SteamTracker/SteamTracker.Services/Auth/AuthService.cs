using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<AuthService> _logger;

    // Логер необов'язковий: у тестах сервіс створюється без нього (NullLogger), DI підставляє справжній
    public AuthService(
        ISteamOpenIdAuthenticator authenticator,
        ISteamApiClient steamApi,
        IUserRepository users,
        ISettingsRepository settings,
        ILogger<AuthService>? logger = null)
    {
        _authenticator = authenticator;
        _steamApi = steamApi;
        _users = users;
        _settings = settings;
        _logger = logger ?? NullLogger<AuthService>.Instance;
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
            _logger.LogError(ex, "Auth: вхід пройшов, але профіль Steam отримати не вдалося");
            throw new AuthFailedException("Не вдалося отримати профіль Steam.", ex);
        }

        if (profile is null)
        {
            // SteamID у лог не пишемо — він є в тексті винятку для UI, у файлі він зайвий
            _logger.LogWarning("Auth: Steam повернув порожній профіль після входу");
            throw new AuthFailedException($"Steam повернув порожній профіль для {steamId}.");
        }

        await _users.UpsertAsync(profile);
        await _settings.SetAsync(SettingsKeys.CurrentSteamId, profile.SteamId);
        SetCurrentUser(profile);
        _logger.LogInformation("Auth: користувач увійшов через Steam");
        return profile;
    }

    public async Task LogoutAsync()
    {
        await _settings.DeleteAsync(SettingsKeys.CurrentSteamId);
        SetCurrentUser(null);
        _logger.LogInformation("Auth: користувач вийшов");
    }

    public async Task<SteamProfile?> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        var steamId = await _settings.GetAsync(SettingsKeys.CurrentSteamId);
        if (string.IsNullOrEmpty(steamId))
            return null;

        var profile = await _users.GetAsync(steamId);
        if (profile is null)
        {
            _logger.LogWarning("Auth: збережений SteamID є, але профілю в локальній БД немає — потрібен повторний вхід");
            return null;
        }

        SetCurrentUser(profile);
        _logger.LogInformation("Auth: сесію відновлено");
        return profile;
    }

    private void SetCurrentUser(SteamProfile? user)
    {
        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }
}