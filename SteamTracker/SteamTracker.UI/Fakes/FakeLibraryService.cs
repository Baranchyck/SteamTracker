using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Фейкова реалізація ILibraryService: бібліотека з Fakes/Data/games.json із справжніми фільтром і сортуванням.
    /// </summary>
    public class FakeLibraryService : ILibraryService
    {
        private static readonly TimeSpan LoadDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>Який результат повертати: ігри, приватний профіль або помилку.</summary>
        public DataStatus Status { get; set; } = DataStatus.Available;

        /// <summary>true — при Status = Available повертає порожню бібліотеку (стан «Порожньо»).</summary>
        public bool ReturnEmpty { get; set; }

        public async Task<ServiceResult<IReadOnlyList<OwnedGame>>> GetMyLibraryAsync(LibraryFilter filter, CancellationToken ct = default)
        {
            // Пауза, щоб на екрані було видно стан «Завантаження»
            await Task.Delay(LoadDelay, ct).ConfigureAwait(false);

            switch (Status)
            {
                case DataStatus.Private:
                    return new ServiceResult<IReadOnlyList<OwnedGame>>(Array.Empty<OwnedGame>(), DataStatus.Private,
                        "Подробиці ігор у профілі приховані.");
                case DataStatus.Error:
                    return new ServiceResult<IReadOnlyList<OwnedGame>>(Array.Empty<OwnedGame>(), DataStatus.Error,
                        "Не вдалося завантажити бібліотеку.");
            }

            if (ReturnEmpty)
            {
                return new ServiceResult<IReadOnlyList<OwnedGame>>(Array.Empty<OwnedGame>(), DataStatus.Available);
            }

            var games = await FakeData.LoadAsync<List<OwnedGame>>("games.json", ct).ConfigureAwait(false);
            return new ServiceResult<IReadOnlyList<OwnedGame>>(Apply(games, filter), DataStatus.Available);
        }

        private static IReadOnlyList<OwnedGame> Apply(IEnumerable<OwnedGame> games, LibraryFilter filter)
        {
            if (filter.OnlyNeverPlayed)
            {
                games = games.Where(g => g.PlaytimeForeverMinutes == 0);
            }

            var sorted = filter.Sort switch
            {
                LibrarySort.NameAsc => games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
                LibrarySort.PlaytimeDesc => games.OrderByDescending(g => g.PlaytimeForeverMinutes),
                // Ігри, які жодного разу не запускали (LastPlayed == null), — у кінці
                LibrarySort.LastPlayedAsc => games.OrderBy(g => g.LastPlayed is null).ThenBy(g => g.LastPlayed),
                _ => throw new ArgumentOutOfRangeException(nameof(filter), filter.Sort, "Unknown sort")
            };

            return sorted.ToList();
        }
    }
}
