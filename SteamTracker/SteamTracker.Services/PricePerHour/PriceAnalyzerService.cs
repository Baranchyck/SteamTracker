using Microsoft.Extensions.Caching.Memory;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.PricePerHour;

public class PriceAnalyzerService : IPriceAnalyzerService
{
    public const int TopSize = 5;

    // Ціни змінюються рідко, а Store має ліміт запитів — тримаємо добу
    private static readonly TimeSpan PriceCacheTtl = TimeSpan.FromHours(24);

    private const int BestValueMinMinutes = 60;      // менше години — шум (купив і раз запустив)
    private const int BarelyPlayedMaxMinutes = 120;

    private const string Note = "Ціна поточна в Steam Store, а не та, за яку гру купили.";

    private readonly ILibraryService _library;
    private readonly IStoreClient _store;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public PriceAnalyzerService(ILibraryService library, IStoreClient store)
    {
        _library = library;
        _store = store;
    }

    public async Task<ServiceResult<PriceAnalysis>> GetAnalysisAsync(CancellationToken ct = default)
    {
        // Бібліотеку беремо з LibraryService — у нього свій кеш, у Steam зайвих запитів не буде
        var library = await _library.GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.PlaytimeDesc), ct);
        if (library.Status != DataStatus.Available || library.Value is null)
            return new(null, library.Status, library.Message);

        var games = library.Value;
        var prices = await GetPricesAsync(games.Select(g => g.AppId), ct);

        var rows = games.Select(g => ToRow(g, prices.GetValueOrDefault(g.AppId))).ToList();

        var analysis = new PriceAnalysis(
            Games: rows,
            BestValue: BestValue(rows),
            ExpensiveBarelyPlayed: ExpensiveBarelyPlayed(rows),
            Note: Note);

        // Ігри, для яких Store нічого не повернув, — Store недоступний (429, мережа): це фолбек, а не помилка
        string? message = games.Any(g => !prices.ContainsKey(g.AppId))
            ? "Не вдалося завантажити ціни зі Steam Store, для частини ігор показано лише час гри."
            : null;

        return new(analysis, DataStatus.Available, message);
    }

    private async Task<Dictionary<int, GamePrice>> GetPricesAsync(IEnumerable<int> appIds, CancellationToken ct)
    {
        var prices = new Dictionary<int, GamePrice>();
        var missing = new List<int>();

        foreach (var appId in appIds)
        {
            if (_cache.TryGetValue(CacheKey(appId), out GamePrice? cached) && cached is not null)
                prices[appId] = cached;
            else
                missing.Add(appId);
        }

        if (missing.Count == 0)
            return prices;

        // Клієнт не кидає винятків: при збої повертає лише те, що встиг отримати
        foreach (var price in await _store.GetPricesAsync(missing, ct))
        {
            _cache.Set(CacheKey(price.AppId), price, PriceCacheTtl);
            prices[price.AppId] = price;
        }

        return prices;
    }

    private static string CacheKey(int appId) => $"price:{appId}";

    private static PricePerHourRow ToRow(OwnedGame game, GamePrice? price)
    {
        bool isFree = price?.IsFree == true;
        int? cents = isFree ? null : price?.FinalCents;
        int minutes = game.PlaytimeForeverMinutes;

        // ціна / (хвилини / 60); при 0 хвилин не ділимо
        decimal? perHour = cents is not null && minutes > 0
            ? cents.Value / (minutes / 60m)
            : null;

        return new PricePerHourRow(
            AppId: game.AppId,
            Name: game.Name,
            PlaytimeMinutes: minutes,
            PriceCents: cents,
            Currency: isFree ? null : price?.Currency,
            IsFree: isFree,
            PricePerHourCents: perHour,
            PriceUnavailable: !isFree && cents is null);
    }

    private static IReadOnlyList<PricePerHourRow> BestValue(IEnumerable<PricePerHourRow> rows) =>
        rows.Where(r => r.PricePerHourCents is not null && r.PlaytimeMinutes >= BestValueMinMinutes)
            .OrderBy(r => r.PricePerHourCents)
            .ThenByDescending(r => r.PlaytimeMinutes)
            .Take(TopSize)
            .ToList();

    private static IReadOnlyList<PricePerHourRow> ExpensiveBarelyPlayed(IReadOnlyList<PricePerHourRow> rows)
    {
        // Безкоштовні й без ціни не беруть участі ні в медіані, ні в топі
        var paid = rows.Where(r => r.PriceCents is not null).ToList();
        if (paid.Count == 0)
            return Array.Empty<PricePerHourRow>();

        var median = Median(paid.Select(r => r.PriceCents!.Value));

        return paid
            .Where(r => r.PriceCents > median && r.PlaytimeMinutes < BarelyPlayedMaxMinutes)
            .OrderByDescending(r => r.PriceCents)
            .ThenBy(r => r.PlaytimeMinutes)
            .Take(TopSize)
            .ToList();
    }

    private static decimal Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2m;
    }
}
