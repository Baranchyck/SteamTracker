using Microsoft.Extensions.Caching.Memory;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.Comparison;

public class ComparisonService : IComparisonService
{
    // Скільки бібліотек друзів вантажиться одночасно (ризик rate limit)
    public const int MaxParallelRequests = 4;

    // Перемикання гри в межах TTL не перезавантажує бібліотеки всіх друзів
    private static readonly TimeSpan LibraryCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public ComparisonService(ISteamApiClient steamApi, IAuthService auth)
    {
        _steamApi = steamApi;
        _auth = auth;
    }

    public async Task<ServiceResult<GameComparison>> GetComparisonAsync(int appId, CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null)
            return new(null, DataStatus.Error, "Спочатку увійдіть через Steam.");

        try
        {
            var userGame = (await GetLibraryAsync(user.SteamId, ct)).FirstOrDefault(g => g.AppId == appId);
            if (userGame is null)
                return new(null, DataStatus.Error, "Цієї гри немає у вашій бібліотеці.");

            var friendIds = await _steamApi.GetFriendIdsAsync(user.SteamId, ct);
            if (friendIds is null)
                return new(null, DataStatus.Private, "Список друзів прихований налаштуваннями приватності.");

            // Неіснуючі SteamID Steam просто не повертає — такі друзі нікуди не потрапляють
            var friends = friendIds.Count == 0
                ? Array.Empty<PlayerPresence>()
                : await _steamApi.GetPlayerSummariesAsync(friendIds, ct);

            using var throttle = new SemaphoreSlim(MaxParallelRequests);
            var libraries = await Task.WhenAll(friends.Select(async f =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    return await GetLibraryAsync(f.SteamId, ct);
                }
                finally
                {
                    throttle.Release();
                }
            }));

            var entries = new List<ComparisonEntry> { ToEntry(user.SteamId, user.PersonaName, user.AvatarUrl, userGame, isUser: true) };
            var withoutGame = new List<SteamProfile>();
            var privateLibrary = new List<SteamProfile>();

            foreach (var (friend, library) in friends.Zip(libraries))
            {
                // TODO(#93): порожній список = прихована бібліотека, поки клієнт не вміє повертати null
                if (library.Count == 0)
                {
                    privateLibrary.Add(ToProfile(friend));
                    continue;
                }

                var game = library.FirstOrDefault(g => g.AppId == appId);
                if (game is null)
                    withoutGame.Add(ToProfile(friend));
                else
                    entries.Add(ToEntry(friend.SteamId, friend.PersonaName, friend.AvatarUrl, game, isUser: false));
            }

            var ranking = Rank(entries);

            var comparison = new GameComparison(
                AppId: appId,
                GameName: userGame.Name,
                Ranking: ranking,
                UserRank: ranking.First(e => e.IsCurrentUser).Rank,
                PercentOfAverage: PercentOfFriendsAverage(entries),
                FriendsWithoutGame: SortByName(withoutGame),
                FriendsWithPrivateLibrary: SortByName(privateLibrary));

            return new(comparison, DataStatus.Available);
        }
        catch (SteamApiException)
        {
            return new(null, DataStatus.Error, "Не вдалося отримати дані друзів зі Steam. Спробуйте пізніше.");
        }
    }

    private async Task<IReadOnlyList<OwnedGame>> GetLibraryAsync(string steamId, CancellationToken ct)
    {
        var key = $"library:{steamId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<OwnedGame>? cached) && cached is not null)
            return cached;

        var library = await _steamApi.GetOwnedGamesAsync(steamId, ct);
        _cache.Set(key, library, LibraryCacheTtl);
        return library;
    }

    // Від більшого часу до меншого; однаковий час — однакове місце (1, 2, 2, 4)
    private static IReadOnlyList<ComparisonEntry> Rank(IEnumerable<ComparisonEntry> entries)
    {
        var sorted = entries
            .OrderByDescending(e => e.PlaytimeForeverMinutes)
            .ThenBy(e => e.PersonaName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var ranked = new List<ComparisonEntry>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            int rank = i > 0 && sorted[i].PlaytimeForeverMinutes == sorted[i - 1].PlaytimeForeverMinutes
                ? ranked[i - 1].Rank
                : i + 1;
            ranked.Add(sorted[i] with { Rank = rank });
        }

        return ranked;
    }

    // Середнє — серед друзів, у кого гра є і час > 0; нікого такого — null, а не ділення на нуль
    private static decimal? PercentOfFriendsAverage(IReadOnlyList<ComparisonEntry> entries)
    {
        var played = entries
            .Where(e => !e.IsCurrentUser && e.PlaytimeForeverMinutes > 0)
            .Select(e => e.PlaytimeForeverMinutes)
            .ToList();

        if (played.Count == 0)
            return null;

        decimal average = (decimal)played.Average();
        int userMinutes = entries.First(e => e.IsCurrentUser).PlaytimeForeverMinutes;
        return Math.Round(userMinutes / average * 100, 1);
    }

    private static ComparisonEntry ToEntry(
        string steamId, string name, string avatarUrl, OwnedGame game, bool isUser) => new(
        SteamId: steamId,
        PersonaName: name,
        AvatarUrl: avatarUrl,
        PlaytimeForeverMinutes: game.PlaytimeForeverMinutes,
        Playtime2WeeksMinutes: game.Playtime2WeeksMinutes,
        Rank: 0, // проставляє Rank()
        IsCurrentUser: isUser);

    private static SteamProfile ToProfile(PlayerPresence p) =>
        new(p.SteamId, p.PersonaName, p.AvatarUrl, p.Visibility);

    private static IReadOnlyList<SteamProfile> SortByName(IEnumerable<SteamProfile> profiles) =>
        profiles.OrderBy(p => p.PersonaName, StringComparer.CurrentCultureIgnoreCase).ToList();
}
