using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IAuthService: вхід без Steam і без мережі, профіль береться з Fakes/Data/profile.json.
    /// </summary>
    public class FakeAuthService : IAuthService
    {
        private static readonly TimeSpan LoginDelay = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan RestoreDelay = TimeSpan.FromMilliseconds(300);

        /// <summary>true — імітує збережену сесію: TryRestoreSessionAsync одразу повертає профіль.</summary>
        public bool HasSavedSession { get; set; }

        /// <summary>true — LoginAsync завершується AuthFailedException.</summary>
        public bool SimulateFailure { get; set; }

        public SteamProfile? CurrentUser { get; private set; }

        public event EventHandler? CurrentUserChanged;

        public async Task<SteamProfile?> TryRestoreSessionAsync(CancellationToken ct = default)
        {
            // ConfigureAwait(false): продовження йде не в UI-потоці, тож подія CurrentUserChanged,
            // як і в справжньому сервісі, приходить з фонового потоку
            await Task.Delay(RestoreDelay, ct).ConfigureAwait(false);

            if (!HasSavedSession)
            {
                return null;
            }

            var profile = await FakeData.LoadAsync<SteamProfile>("profile.json", ct).ConfigureAwait(false);
            SetCurrentUser(profile);
            return profile;
        }

        public async Task<SteamProfile> LoginAsync(CancellationToken ct = default)
        {
            try
            {
                // Пауза імітує очікування підтвердження входу в браузері
                await Task.Delay(LoginDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new AuthCancelledException("Login was cancelled.", ex);
            }

            if (SimulateFailure)
            {
                throw new AuthFailedException("Fake login failure.");
            }

            var profile = await FakeData.LoadAsync<SteamProfile>("profile.json", CancellationToken.None).ConfigureAwait(false);
            SetCurrentUser(profile);
            return profile;
        }

        public Task LogoutAsync()
        {
            SetCurrentUser(null);
            return Task.CompletedTask;
        }

        private void SetCurrentUser(SteamProfile? profile)
        {
            CurrentUser = profile;
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
