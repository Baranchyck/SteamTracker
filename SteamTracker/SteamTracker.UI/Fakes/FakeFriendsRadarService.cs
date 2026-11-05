using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.FriendsRadar;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IFriendsRadarService: друзі в усіх статусах прямо в коді (як у макеті «05 · Друзі-радар»).
    /// </summary>
    public class FakeFriendsRadarService : IFriendsRadarService
    {
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(500);

        // internal: ті самі друзі потрібні FakeMatchmakerService, щоб ніки й SteamID збігалися
        internal static readonly IReadOnlyList<PlayerPresence> Friends = new[]
        {
            F("76561198000000001", "Андрій", PersonaState.Online, 900001, "Starfall Tactics"),
            F("76561198000000002", "Марта", PersonaState.Online, 548430, "Deep Rock Galactic"),
            F("76561198000000003", "Денис", PersonaState.LookingToPlay, 570, "Dota 2"),
            F("76561198000000004", "Олег", PersonaState.Online),
            F("76561198000000005", "Софія", PersonaState.LookingToPlay),
            F("76561198000000006", "Тарас", PersonaState.Online),
            F("76561198000000007", "Ірина", PersonaState.Away),
            F("76561198000000008", "Віка", PersonaState.Offline),
            F("76561198000000009", "Максим", PersonaState.Offline),
            F("76561198000000010", "Настя", PersonaState.Offline),
            F("76561198000000011", "Богдан", PersonaState.Offline),
            F("76561198000000012", "Ліза", PersonaState.Offline)
        };

        /// <summary>Який результат повертати: друзі, прихований список друзів або помилку.</summary>
        public DataStatus Status { get; set; } = DataStatus.Available;

        /// <summary>true — при Status = Available друзів немає (стан «Порожньо»).</summary>
        public bool ReturnEmpty { get; set; }

        public async Task<ServiceResult<IReadOnlyList<PlayerPresence>>> GetFriendsAsync(RadarFilter filter, CancellationToken ct = default)
        {
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            switch (Status)
            {
                case DataStatus.Private:
                    return new ServiceResult<IReadOnlyList<PlayerPresence>>(Array.Empty<PlayerPresence>(), DataStatus.Private,
                        "Список друзів прихований.");
                case DataStatus.Error:
                    return new ServiceResult<IReadOnlyList<PlayerPresence>>(Array.Empty<PlayerPresence>(), DataStatus.Error,
                        "Не вдалося завантажити друзів.");
            }

            IEnumerable<PlayerPresence> friends = ReturnEmpty ? Array.Empty<PlayerPresence>() : Friends;

            // Ті самі правила, що в FriendsRadarService (контракт IFriendsRadarService і RadarFilter)
            if (filter.OnlyAvailable)
            {
                friends = friends.Where(FriendStatus.IsAvailable);
            }

            IReadOnlyList<PlayerPresence> sorted = friends
                .OrderBy(FriendStatus.GroupOf)
                .ThenBy(f => f.PersonaName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new ServiceResult<IReadOnlyList<PlayerPresence>>(sorted, DataStatus.Available);
        }

        private static PlayerPresence F(string steamId, string name, PersonaState state, int? gameId = null, string? gameName = null) =>
            new(steamId, name, "", ProfileVisibility.Public, state, gameId, gameName);
    }
}
