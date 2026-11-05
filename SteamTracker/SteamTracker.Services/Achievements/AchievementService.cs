using Microsoft.Extensions.Caching.Memory;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.Achievements;

public class AchievementService : IAchievementService
{
    // Скільки запитів GetPlayerAchievements летить у Steam одночасно
    public const int MaxParallelRequests = 4;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Глобальні % однакові для всіх і змінюються повільно — тримаємо довше
    private static readonly TimeSpan GlobalCacheTtl = TimeSpan.FromHours(1);

    private readonly ISteamApiClient _steamApi;
    private readonly IAuthService _auth;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public AchievementService(ISteamApiClient steamApi, IAuthService auth)
    {
        _steamApi = steamApi;
        _auth = auth;
    }

    public async Task<ServiceResult<IReadOnlyList<AchievementGameProgress>>> GetGamesInProgressAsync(
        CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null)
            return new(null, DataStatus.Error, "Спочатку увійдіть через Steam.");

        try
        {
            var games = await GetOwnedGamesAsync(user.SteamId, ct);

            // TODO(#93): порожній список = приватний профіль, поки клієнт не вміє повертати null
            if (games.Count == 0)
                return new(null, DataStatus.Private, "Бібліотека порожня або профіль приватний.");

            // Тільки ігри зі статистикою: для решти Steam однаково відповість "no stats"
            var candidates = games.Where(g => g.HasCommunityVisibleStats).ToList();
            if (candidates.Count == 0)
                return new(Array.Empty<AchievementGameProgress>(), DataStatus.Available);

            using var throttle = new SemaphoreSlim(MaxParallelRequests);
            var loaded = await Task.WhenAll(candidates.Select(async game =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    return (Game: game, Achievements: await GetAchievementsAsync(user.SteamId, game.AppId, ct));
                }
                finally
                {
                    throttle.Release();
                }
            }));

            // Деталі ігор закриті для всіх ігор одразу — це налаштування профілю, а не конкретної гри
            if (loaded.All(x => x.Achievements is null))
                return new(null, DataStatus.Private,
                    "Статистика ігор прихована в налаштуваннях приватності Steam.");

            var inProgress = loaded
                .Where(x => x.Achievements is not null)
                .Select(x => ToProgress(x.Game, x.Achievements!))
                .Where(p => p.Achieved > 0 && p.Achieved < p.Total)
                .OrderByDescending(p => p.Percent)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new(inProgress, DataStatus.Available);
        }
        catch (SteamApiException)
        {
            return new(null, DataStatus.Error, "Не вдалося отримати ачівки зі Steam. Спробуйте пізніше.");
        }
    }

    public async Task<ServiceResult<AchievementPlan>> GetPlanAsync(int appId, CancellationToken ct = default)
    {
        var user = _auth.CurrentUser;
        if (user is null)
            return new(null, DataStatus.Error, "Спочатку увійдіть через Steam.");

        if (appId <= 0)
            return new(null, DataStatus.Error, "Некоректний ідентифікатор гри.");

        IReadOnlyList<PlayerAchievement>? achievements;
        try
        {
            achievements = await GetAchievementsAsync(user.SteamId, appId, ct);
        }
        catch (SteamApiException)
        {
            return new(null, DataStatus.Error, "Не вдалося отримати ачівки зі Steam. Спробуйте пізніше.");
        }

        if (achievements is null)
            return new(null, DataStatus.Private,
                "Статистика гри прихована в налаштуваннях приватності Steam.");

        if (achievements.Count == 0)
            return new(new AchievementPlan(appId, 0, 0, Array.Empty<PlannedAchievement>()), DataStatus.Available);

        // Без глобальних % план усе одно корисний (список недосягнутих), просто без сортування за складністю
        IReadOnlyDictionary<string, double> global;
        string? message = null;
        try
        {
            global = await GetGlobalPercentagesAsync(appId, ct);
        }
        catch (SteamApiException)
        {
            global = new Dictionary<string, double>();
            message = "Не вдалося завантажити глобальну статистику, ачівки не відсортовані за складністю.";
        }

        var remaining = SortByEase(achievements
            .Where(a => !a.Achieved)
            .Select(a => new PlannedAchievement(
                a.ApiName,
                a.DisplayName,
                a.Description,
                global.TryGetValue(a.ApiName, out var percent) ? percent : null)));

        var plan = new AchievementPlan(
            AppId: appId,
            Achieved: achievements.Count(a => a.Achieved),
            Total: achievements.Count,
            Remaining: remaining);

        return new(plan, DataStatus.Available, message);
    }

    public RoadmapResult BuildRoadmap(AchievementPlan plan, int targetPointsUp)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (targetPointsUp < 0)
            throw new ArgumentOutOfRangeException(nameof(targetPointsUp), "Кількість пунктів не може бути від'ємною.");

        // +N пунктів = N% від загальної кількості ачівок; дробову ачівку не закриєш, тому вгору
        int needed = (int)Math.Ceiling(targetPointsUp * plan.Total / 100m);
        int take = Math.Min(needed, plan.Remaining.Count);

        // Remaining уже відсортований від найлегших, але план міг прийти не з GetPlanAsync
        var picked = SortByEase(plan.Remaining).Take(take).ToList();

        var after = plan with { Achieved = plan.Achieved + picked.Count };

        return new RoadmapResult(
            TargetPointsUp: targetPointsUp,
            Achievements: picked,
            CurrentPercent: plan.Percent,
            NewPercent: after.Percent);
    }

    // Найлегші (найбільший глобальний %) — зверху; без % — у кінці; далі за назвою для стабільного порядку
    private static IReadOnlyList<PlannedAchievement> SortByEase(IEnumerable<PlannedAchievement> achievements) =>
        achievements
            .OrderBy(a => a.GlobalPercent is null)
            .ThenByDescending(a => a.GlobalPercent)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static AchievementGameProgress ToProgress(OwnedGame game, IReadOnlyList<PlayerAchievement> achievements) =>
        new(game.AppId, game.Name, game.IconUrl, achievements.Count(a => a.Achieved), achievements.Count);

    private async Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(string steamId, CancellationToken ct)
    {
        var key = $"games:{steamId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<OwnedGame>? cached) && cached is not null)
            return cached;

        var games = await _steamApi.GetOwnedGamesAsync(steamId, ct);
        if (games.Count > 0)
            _cache.Set(key, games, CacheTtl);
        return games;
    }

    // null (приватно) не кешуємо: користувач міг щойно відкрити профіль
    private async Task<IReadOnlyList<PlayerAchievement>?> GetAchievementsAsync(
        string steamId, int appId, CancellationToken ct)
    {
        var key = $"achievements:{steamId}:{appId}";
        if (_cache.TryGetValue(key, out IReadOnlyList<PlayerAchievement>? cached) && cached is not null)
            return cached;

        var achievements = await _steamApi.GetPlayerAchievementsAsync(steamId, appId, ct);
        if (achievements is not null)
            _cache.Set(key, achievements, CacheTtl);
        return achievements;
    }

    // Ключ без SteamID: глобальні % однакові для всіх гравців
    private async Task<IReadOnlyDictionary<string, double>> GetGlobalPercentagesAsync(int appId, CancellationToken ct)
    {
        var key = $"global:{appId}";
        if (_cache.TryGetValue(key, out IReadOnlyDictionary<string, double>? cached) && cached is not null)
            return cached;

        var global = await _steamApi.GetGlobalAchievementPercentagesAsync(appId, ct);
        _cache.Set(key, global, GlobalCacheTtl);
        return global;
    }
}
