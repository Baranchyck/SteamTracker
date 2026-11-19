using Microsoft.Extensions.Caching.Memory;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.Common;

namespace SteamTracker.Services.Library;

public class LibraryService : ILibraryService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public LibraryService(ISteamApiClient steamApi, IAuthService auth)
    {
        _steamApi = steamApi;
        _auth = auth;
    }

    public async Task<ServiceResult<IReadOnlyList<OwnedGame>>> GetMyLibraryAsync(
    LibraryFilter filter, CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null)
            return ServiceCall.NotSignedIn<IReadOnlyList<OwnedGame>>();

        var cacheKey = $"library:{user.SteamId}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<OwnedGame>? cached) && cached is not null)
            return new(ApplyFilter(cached, filter), DataStatus.Available);

        return await ServiceCall.RunAsync<IReadOnlyList<OwnedGame>>(async () =>
        {
            var games = await _steamApi.GetOwnedGamesAsync(user.SteamId, ct);

            if (games is null)
                return ServiceCall.Private<IReadOnlyList<OwnedGame>>(
                    "Бібліотека прихована налаштуваннями приватності Steam.");

            _cache.Set(cacheKey, games, CacheTtl);
            return new(ApplyFilter(games, filter), DataStatus.Available);
        }, "Не вдалося отримати бібліотеку зі Steam. Спробуйте пізніше.");
    }

    private static IReadOnlyList<OwnedGame> ApplyFilter(
    IReadOnlyList<OwnedGame> games, LibraryFilter filter)
    {
        IEnumerable<OwnedGame> query = games;

        if (filter.OnlyNeverPlayed)
            query = query.Where(g => g.PlaytimeForeverMinutes == 0);

        query = filter.Sort switch
        {
            LibrarySort.NameAsc =>
                query.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase),
            LibrarySort.PlaytimeDesc =>
                query.OrderByDescending(g => g.PlaytimeForeverMinutes)
                     .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase),
            LibrarySort.LastPlayedAsc =>
                query.OrderBy(g => g.LastPlayed == null)
                     .ThenBy(g => g.LastPlayed),
            _ => query
        };

        return query.ToList();
    }
}