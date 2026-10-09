using SteamTracker.Services.Models;

namespace SteamTracker.Services.Abstractions;

public interface IAuthService
{
    SteamProfile? CurrentUser { get; }
    event EventHandler? CurrentUserChanged;   // може прийти не з UI-потоку

    Task<SteamProfile?> TryRestoreSessionAsync(CancellationToken ct = default);
    Task<SteamProfile> LoginAsync(CancellationToken ct = default);
    Task LogoutAsync();
}