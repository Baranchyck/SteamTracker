using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація IMatchmakerService: бібліотеки друзів прямо в коді, ті самі правила, що в MatchmakerService
    /// (до 8 учасників, невалідні вводи окремо, прихована бібліотека не обнуляє перетин).
    /// Дані підігнані під макет «06 · Co-op Matchmaker»: ти + Андрій + Марта дають 142 / 118 / 96 / 41 / 12 / 9 год,
    /// а в Олега бібліотека прихована.
    /// </summary>
    public class FakeMatchmakerService : IMatchmakerService
    {
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(500);

        private readonly IAuthService _auth;

        // Ігри: appid → назва
        private static readonly Dictionary<int, string> Titles = new()
        {
            [548430] = "Deep Rock Galactic",
            [730] = "Counter-Strike 2",
            [900001] = "Starfall Tactics",
            [550] = "Left 4 Dead 2",
            [620] = "Portal 2",
            [105600] = "Terraria",
            [570] = "Dota 2",
            [1086940] = "Baldur's Gate 3"
        };

        // Години гри в кожній грі. Ключ — SteamID друга з FakeFriendsRadarService; порожня бібліотека = прихована
        private static readonly Dictionary<string, Dictionary<int, int>> FriendLibraries = new()
        {
            ["76561198000000001"] = new() { [548430] = 90, [730] = 140, [900001] = 100, [550] = 30, [620] = 8, [105600] = 6 },
            ["76561198000000002"] = new() { [548430] = 126, [730] = 95, [900001] = 68, [550] = 33, [620] = 13, [105600] = 10, [570] = 40 },
            ["76561198000000003"] = new() { [570] = 800, [730] = 50, [105600] = 40 },
            ["76561198000000004"] = new(),
            ["76561198000000005"] = new() { [548430] = 50, [105600] = 3, [620] = 20, [1086940] = 140 },
            ["76561198000000006"] = new() { [730] = 200, [570] = 500, [550] = 10, [548430] = 20 }
        };

        // Бібліотека поточного користувача
        private static readonly Dictionary<int, int> UserLibrary = new()
        {
            [548430] = 210, [730] = 120, [900001] = 120, [550] = 60, [620] = 15, [105600] = 12, [570] = 300, [1086940] = 80
        };

        // Решта друзів з радара: невелика типова бібліотека
        private static readonly Dictionary<int, int> DefaultLibrary = new() { [105600] = 5, [620] = 4 };

        public FakeMatchmakerService(IAuthService auth)
        {
            _auth = auth;
        }

        /// <summary>Який результат повертати: дані або помилку.</summary>
        public DataStatus Status { get; set; } = DataStatus.Available;

        /// <summary>true — при Status = Available спільних ігор немає (стан «Спільних ігор не знайдено»).</summary>
        public bool ReturnEmpty { get; set; }

        public async Task<ServiceResult<MatchResult>> FindCommonGamesAsync(
            IReadOnlyList<string> participantInputs, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(participantInputs);

            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            var user = _auth.CurrentUser;
            if (user is null)
            {
                return new(null, DataStatus.Error, "Спочатку увійдіть через Steam.");
            }

            if (Status == DataStatus.Error)
            {
                return new(null, DataStatus.Error, "Не вдалося отримати дані зі Steam. Спробуйте пізніше.");
            }

            var inputs = participantInputs.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (inputs.Count > IMatchmakerService.MaxParticipants * 2)
            {
                return new(null, DataStatus.Error,
                    $"Забагато учасників: максимум {IMatchmakerService.MaxParticipants} разом з вами.");
            }

            var ids = new List<string> { user.SteamId };
            var invalid = new List<SteamIdResolveResult>();
            foreach (var input in inputs)
            {
                var resolved = Resolve(input);
                if (!resolved.IsSuccess)
                {
                    invalid.Add(resolved);
                }
                else if (!ids.Contains(resolved.SteamId!))
                {
                    ids.Add(resolved.SteamId!);
                }
            }

            if (ids.Count < IMatchmakerService.MinParticipants)
            {
                return new(null, DataStatus.Error, invalid.Count > 0
                    ? $"Немає жодного іншого учасника: {invalid[0].Message}"
                    : "Додайте хоча б одного учасника.");
            }

            if (ids.Count > IMatchmakerService.MaxParticipants)
            {
                return new(null, DataStatus.Error,
                    $"Забагато учасників: максимум {IMatchmakerService.MaxParticipants} разом з вами.");
            }

            var libraries = ids.Select(id => LibraryOf(id, user)).ToList();
            var participants = ids.Select((id, i) => ToParticipant(id, user, libraryHidden: libraries[i].Count == 0)).ToList();
            var hidden = participants.Where(p => p.LibraryHidden).ToList();

            string? message = null;
            IReadOnlyList<CommonGame> common = Array.Empty<CommonGame>();
            var visible = ids.Zip(libraries).Where(x => x.Second.Count > 0).ToList();

            if (visible.Count < IMatchmakerService.MinParticipants)
            {
                message = "Недостатньо відкритих бібліотек, щоб знайти спільні ігри.";
            }
            else if (!ReturnEmpty)
            {
                common = Intersect(visible);
            }

            return new(new MatchResult(participants, common, invalid, hidden), DataStatus.Available, message);
        }

        private static IReadOnlyList<CommonGame> Intersect(List<(string SteamId, Dictionary<int, int> Hours)> visible)
        {
            var commonIds = visible
                .Select(v => (IEnumerable<int>)v.Hours.Keys)
                .Aggregate((acc, keys) => acc.Intersect(keys))
                .ToList();

            return commonIds
                .Select(appId =>
                {
                    var minutes = visible.ToDictionary(v => v.SteamId, v => v.Hours[appId] * 60);
                    return new CommonGame(
                        appId,
                        Titles[appId],
                        // Без картинки: у фейку лишається сіра заглушка, як у макеті
                        "",
                        Math.Round((decimal)minutes.Values.Average(), 1),
                        minutes);
                })
                .OrderByDescending(g => g.AveragePlaytimeMinutes)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static SteamIdResolveResult Resolve(string input)
        {
            string text = input.Trim();

            var friend = FakeFriendsRadarService.Friends.FirstOrDefault(f =>
                f.SteamId == text || string.Equals(f.PersonaName, text, StringComparison.CurrentCultureIgnoreCase));
            if (friend is not null)
            {
                return SteamIdResolveResult.Ok(input, friend.SteamId);
            }

            if (text.Length == 17 && text.All(char.IsDigit))
            {
                return SteamIdResolveResult.Fail(input, SteamIdResolveError.NotFound, "Профіль не знайдено.");
            }

            return text.Contains(' ')
                ? SteamIdResolveResult.Fail(input, SteamIdResolveError.InvalidFormat, "Це не схоже на SteamID, посилання чи нік.")
                : SteamIdResolveResult.Fail(input, SteamIdResolveError.NotFound, "Профіль не знайдено.");
        }

        private static Dictionary<int, int> LibraryOf(string steamId, SteamProfile user) =>
            steamId == user.SteamId ? UserLibrary
            : FriendLibraries.GetValueOrDefault(steamId) ?? DefaultLibrary;

        private static MatchParticipant ToParticipant(string steamId, SteamProfile user, bool libraryHidden)
        {
            if (steamId == user.SteamId)
            {
                return new(steamId, user.PersonaName, user.AvatarUrl, PersonaState.Online, true, libraryHidden);
            }

            var friend = FakeFriendsRadarService.Friends.First(f => f.SteamId == steamId);
            return new(steamId, friend.PersonaName, friend.AvatarUrl, friend.PersonaState, false, libraryHidden);
        }
    }
}
