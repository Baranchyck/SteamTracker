using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IComparisonService: друзі прямо в коді (як у макеті «04 · Час гри з друзями»).
    /// Гра й час користувача беруться з фейкової бібліотеки, час друзів — пропорційно до нього.
    /// </summary>
    public class FakeComparisonService : IComparisonService
    {
        // Довше, ніж у інших фейків: справжній сервіс вантажить бібліотеки всіх друзів
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(800);

        // Якщо користувач гру не запускав, час друзів рахується від цього значення
        private const int FallbackMinutes = 600;

        // Ім'я, частка від часу користувача, хвилини за 2 тижні
        private static readonly (string SteamId, string Name, double Factor, int TwoWeeks)[] FriendsWithGame =
        {
            ("76561198000000001", "Андрій", 1.68, 840),
            ("76561198000000002", "Марта", 1.25, 0),
            ("76561198000000004", "Олег", 0.77, 180),
            ("76561198000000005", "Софія", 0.51, 0),
            ("76561198000000003", "Денис", 0.25, 0),
            ("76561198000000007", "Ірина", 0.06, 0)
        };

        private static readonly IReadOnlyList<SteamProfile> FriendsWithoutGame = new[]
        {
            P("76561198000000006", "Тарас"),
            P("76561198000000008", "Віка"),
            P("76561198000000009", "Максим"),
            P("76561198000000010", "Настя"),
            P("76561198000000011", "Богдан")
        };

        private static readonly IReadOnlyList<SteamProfile> FriendsWithPrivateLibrary = new[]
        {
            P("76561198000000012", "Ліза")
        };

        private readonly ILibraryService _library;
        private readonly IAuthService _auth;

        public FakeComparisonService(ILibraryService library, IAuthService auth)
        {
            _library = library;
            _auth = auth;
        }

        /// <summary>Який результат повертати: порівняння, прихований список друзів або помилку.</summary>
        public DataStatus Status { get; set; } = DataStatus.Available;

        /// <summary>true — при Status = Available друзів немає (стан «Порожньо»).</summary>
        public bool ReturnEmpty { get; set; }

        public async Task<ServiceResult<GameComparison>> GetComparisonAsync(int appId, CancellationToken ct = default)
        {
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            switch (Status)
            {
                case DataStatus.Private:
                    return new ServiceResult<GameComparison>(null, DataStatus.Private, "Список друзів прихований.");
                case DataStatus.Error:
                    return new ServiceResult<GameComparison>(null, DataStatus.Error, "Не вдалося завантажити дані друзів.");
            }

            var library = await _library.GetMyLibraryAsync(new LibraryFilter(), ct).ConfigureAwait(false);
            var game = library.Value?.FirstOrDefault(g => g.AppId == appId);
            if (game is null)
            {
                return new ServiceResult<GameComparison>(null, DataStatus.Error, "Цієї гри немає у вашій бібліотеці.");
            }

            var me = _auth.CurrentUser;
            var entries = new List<ComparisonEntry>
            {
                new(me?.SteamId ?? "me", me?.PersonaName ?? "player", me?.AvatarUrl ?? "",
                    game.PlaytimeForeverMinutes, game.Playtime2WeeksMinutes, 0, IsCurrentUser: true)
            };

            if (!ReturnEmpty)
            {
                int baseMinutes = game.PlaytimeForeverMinutes > 0 ? game.PlaytimeForeverMinutes : FallbackMinutes;
                entries.AddRange(FriendsWithGame.Select(f => new ComparisonEntry(
                    f.SteamId, f.Name, "", (int)(baseMinutes * f.Factor), f.TwoWeeks, 0, IsCurrentUser: false)));
            }

            // Ті самі правила, що в ComparisonService: однаковий час — однакове місце, середнє — серед друзів з часом > 0
            var sorted = entries
                .OrderByDescending(e => e.PlaytimeForeverMinutes)
                .ThenBy(e => e.PersonaName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var ranking = sorted
                .Select(e => e with { Rank = sorted.Count(o => o.PlaytimeForeverMinutes > e.PlaytimeForeverMinutes) + 1 })
                .ToList();

            var played = ranking.Where(e => !e.IsCurrentUser && e.PlaytimeForeverMinutes > 0).ToList();
            decimal? percent = played.Count == 0
                ? null
                : Math.Round(game.PlaytimeForeverMinutes / (decimal)played.Average(e => e.PlaytimeForeverMinutes) * 100, 1);

            var comparison = new GameComparison(
                AppId: appId,
                GameName: game.Name,
                Ranking: ranking,
                UserRank: ranking.First(e => e.IsCurrentUser).Rank,
                PercentOfAverage: percent,
                FriendsWithoutGame: ReturnEmpty ? Array.Empty<SteamProfile>() : FriendsWithoutGame,
                FriendsWithPrivateLibrary: ReturnEmpty ? Array.Empty<SteamProfile>() : FriendsWithPrivateLibrary);

            return new ServiceResult<GameComparison>(comparison, DataStatus.Available);
        }

        private static SteamProfile P(string steamId, string name) => new(steamId, name, "", ProfileVisibility.Public);
    }
}
