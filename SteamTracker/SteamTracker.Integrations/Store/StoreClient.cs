using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.Store.Dtos;

namespace SteamTracker.Integrations.Store;

public class StoreClient : IStoreClient
{
    // Кілька appid в одному запиті Store приймає лише з filters=price_overview.
    internal const int BatchSize = 50;

    private readonly HttpClient _httpClient;
    private readonly string _countryCode;

    // countryCode визначає регіон і валюту цін ("ua" -> UAH).
    public StoreClient(HttpClient httpClient, string countryCode = "ua")
    {
        _httpClient = httpClient;
        _countryCode = countryCode;
    }

    public async Task<IReadOnlyList<GamePrice>> GetPricesAsync(
        IEnumerable<int> appIds, CancellationToken ct = default)
    {
        var result = new List<GamePrice>();

        foreach (var batch in appIds.Distinct().Chunk(BatchSize))
        {
            var prices = await TryGetBatchAsync(batch, ct);

            // 429 або інший збій: наступні пачки теж не пройдуть, віддаємо те, що вже є
            if (prices is null)
                break;

            result.AddRange(prices);
        }

        return result;
    }

    private async Task<List<GamePrice>?> TryGetBatchAsync(int[] appIds, CancellationToken ct)
    {
        var url = "https://store.steampowered.com/api/appdetails"
                  + $"?appids={string.Join(',', appIds)}"
                  + "&filters=price_overview"
                  + $"&cc={Uri.EscapeDataString(_countryCode)}";

        Dictionary<string, AppDetailsEntry>? response;
        try
        {
            using var http = await _httpClient.GetAsync(url, ct);

            // Сюди ж потрапляє 429 (ліміт ~200 запитів / 5 хв)
            if (!http.IsSuccessStatusCode)
                return null;

            response = await http.Content.ReadFromJsonAsync<Dictionary<string, AppDetailsEntry>>(ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // таймаут
        }

        if (response is null)
            return null;

        var prices = new List<GamePrice>();
        foreach (var appId in appIds)
        {
            if (response.TryGetValue(appId.ToString(CultureInfo.InvariantCulture), out var entry))
                prices.Add(ToDomain(appId, entry));
        }

        return prices;
    }

    private static GamePrice ToDomain(int appId, AppDetailsEntry entry)
    {
        // success: false — гра недоступна в регіоні або appid не існує
        if (!entry.Success)
            return new GamePrice(appId, FinalCents: null, Currency: null, IsFree: false);

        // Немає price_overview ("data": []) — гра безкоштовна
        if (entry.Data.ValueKind != JsonValueKind.Object
            || !entry.Data.TryGetProperty("price_overview", out var price)
            || price.ValueKind != JsonValueKind.Object)
            return new GamePrice(appId, FinalCents: null, Currency: null, IsFree: true);

        int? finalCents = price.TryGetProperty("final", out var final) && final.TryGetInt32(out var cents)
            ? cents
            : null;
        string? currency = price.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
            ? cur.GetString()
            : null;

        return new GamePrice(appId, finalCents, currency, IsFree: false);
    }
}
