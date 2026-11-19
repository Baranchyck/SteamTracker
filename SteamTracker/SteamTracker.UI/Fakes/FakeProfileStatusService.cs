using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IProfileStatusService: поточний користувач у грі.
    /// </summary>
    public class FakeProfileStatusService : IProfileStatusService
    {
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(300);

        private readonly IAuthService _auth;

        public FakeProfileStatusService(IAuthService auth)
        {
            _auth = auth;
        }

        /// <summary>null — імітує недоступний статус.</summary>
        public PersonaState? State { get; set; } = PersonaState.Online;

        public string? GameName { get; set; } = "Deep Rock Galactic";

        public async Task<PlayerPresence?> GetCurrentStatusAsync(CancellationToken ct = default)
        {
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            var user = _auth.CurrentUser;
            if (user is null || State is null)
            {
                return null;
            }

            return new PlayerPresence(user.SteamId, user.PersonaName, user.AvatarUrl, user.Visibility,
                State.Value, GameName is null ? null : 548430, GameName);
        }
    }
}
