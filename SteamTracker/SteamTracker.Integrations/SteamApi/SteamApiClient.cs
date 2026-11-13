using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.SteamApi.Dtos;

namespace SteamTracker.Integrations.SteamApi;

public class SteamApiClient : ISteamApiClient
{
    private const string BaseUrl = "https://api.steampowered.com";

    private const string IconBaseUrl =
        "https://media.steampowered.com/steamcommunity/public/images/apps";

    // GetPlayerSummaries приймає не більше 100 SteamID за запит
    internal const int SummariesBatchSize = 100;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public SteamApiClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default)
    {
        var envelope = await GetJsonAsync<GetPlayerSummariesEnvelope>(SummariesUrl(steamId), ct);

        // Неіснуючий SteamId: "players": []
        var player = envelope?.Response?.Players?.FirstOrDefault();
        if (player is null)
            return null;

        return new SteamProfile(
            SteamId: player.SteamId ?? steamId,
            PersonaName: player.PersonaName ?? string.Empty,
            AvatarUrl: player.AvatarFull ?? string.Empty,
            Visibility: ToVisibility(player.CommunityVisibilityState),
            TimeCreated: player.TimeCreated is long created and > 0
                ? DateTimeOffset.FromUnixTimeSeconds(created)
                : null);
    }

    public async Task<IReadOnlyList<OwnedGame>?> GetOwnedGamesAsync(
        string steamId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/IPlayerService/GetOwnedGames/v1/"
                  + $"?key={Uri.EscapeDataString(_apiKey)}"
                  + $"&steamid={Uri.EscapeDataString(steamId)}"
                  + "&include_appinfo=true&include_played_free_games=true&format=json";

        var envelope = await GetJsonAsync<GetOwnedGamesEnvelope>(url, ct);

        if (envelope?.Response?.GameCount is null)
            return null;

        var games = envelope.Response.Games;
        if (games is null)
            return Array.Empty<OwnedGame>();

        return games.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<string>?> GetFriendIdsAsync(
        string steamId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/ISteamUser/GetFriendList/v1/"
                  + $"?key={Uri.EscapeDataString(_apiKey)}"
                  + $"&steamid={Uri.EscapeDataString(steamId)}"
                  + "&relationship=friend&format=json";

        // Закритий список друзів Steam віддає як HTTP 401 — це не помилка, а стан приватності
        var envelope = await GetJsonAsync<GetFriendListEnvelope>(url, ct, nullOnStatus: HttpStatusCode.Unauthorized);
        if (envelope is null)
            return null;

        // Відкритий профіль без друзів: friendslist або friends відсутні
        return envelope.FriendsList?.Friends?
                   .Select(f => f.SteamId)
                   .OfType<string>()
                   .ToList()
               ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public async Task<IReadOnlyList<PlayerPresence>> GetPlayerSummariesAsync(
        IEnumerable<string> steamIds, CancellationToken ct = default)
    {
        var ids = steamIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var result = new List<PlayerPresence>(ids.Count);

        // Пачки йдуть послідовно: їх максимум кілька, а паралельність тут лише ризикує лімітами Steam
        foreach (var batch in ids.Chunk(SummariesBatchSize))
        {
            var envelope = await GetJsonAsync<GetPlayerSummariesEnvelope>(SummariesUrl(batch), ct);
            var players = envelope?.Response?.Players;
            if (players is null)
                continue;

            result.AddRange(players
                .Where(p => !string.IsNullOrEmpty(p.SteamId))
                .Select(ToPresence));
        }

        return result;
    }

    public async Task<IReadOnlyList<PlayerAchievement>?> GetPlayerAchievementsAsync(
        string steamId, int appId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/ISteamUserStats/GetPlayerAchievements/v1/"
                  + $"?key={Uri.EscapeDataString(_apiKey)}"
                  + $"&steamid={Uri.EscapeDataString(steamId)}"
                  + $"&appid={appId}"
                  + "&l=ukrainian&format=json";

        // 403 — профіль або деталі ігор приховані.
        // 400 — тіло з поясненням: "no stats" означає гру без ачівок, решта — справжня помилка.
        var envelope = await GetJsonAsync<GetPlayerAchievementsEnvelope>(
            url, ct, nullOnStatus: HttpStatusCode.Forbidden, readBodyOnStatus: HttpStatusCode.BadRequest);
        if (envelope is null)
            return null;

        var stats = envelope.PlayerStats
                    ?? throw new SteamApiException("Steam API повернув некоректну відповідь.");

        if (!stats.Success)
        {
            if (IsNoStatsError(stats.Error))
                return Array.Empty<PlayerAchievement>();

            // success: false з кодом 200 Steam теж повертає для закритих профілів
            if (IsNotPublicError(stats.Error))
                return null;

            throw new SteamApiException($"Steam API повернув помилку: {stats.Error ?? "невідома"}.");
        }

        // Статистика є, але ачівок у гри немає — поля achievements просто нема
        return stats.Achievements?
                   .Where(a => !string.IsNullOrEmpty(a.ApiName))
                   .Select(ToAchievement)
                   .ToList()
               ?? (IReadOnlyList<PlayerAchievement>)Array.Empty<PlayerAchievement>();
    }

    public async Task<IReadOnlyDictionary<string, double>> GetGlobalAchievementPercentagesAsync(
        int appId, CancellationToken ct = default)
    {
        // Публічний метод: ключ не потрібен
        var url = $"{BaseUrl}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/"
                  + $"?gameid={appId}&format=json";

        var envelope = await GetJsonAsync<GlobalPercentagesEnvelope>(url, ct);

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var a in envelope?.AchievementPercentages?.Achievements ?? Enumerable.Empty<GlobalPercentageDto>())
        {
            if (!string.IsNullOrEmpty(a.Name))
                result[a.Name] = a.Percent;
        }

        return result;
    }

    public async Task<string?> ResolveVanityUrlAsync(string vanity, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/ISteamUser/ResolveVanityURL/v1/"
                  + $"?key={Uri.EscapeDataString(_apiKey)}"
                  + $"&vanityurl={Uri.EscapeDataString(vanity)}"
                  + "&url_type=1&format=json"; // 1 — профіль гравця (не група)

        var envelope = await GetJsonAsync<ResolveVanityUrlEnvelope>(url, ct);
        var response = envelope?.Response
                       ?? throw new SteamApiException("Steam API повернув некоректну відповідь.");

        return response.Success switch
        {
            1 when !string.IsNullOrEmpty(response.SteamId) => response.SteamId,
            42 => null,
            _ => throw new SteamApiException(
                $"Steam API повернув помилку: {response.Message ?? $"success={response.Success}"}.")
        };
    }

    // Один шлях для всіх запитів: мережа / не-2xx / таймаут / битий JSON → SteamApiException.
    // nullOnStatus — код, який для конкретного методу означає "даних немає", а не помилку.
    // readBodyOnStatus — не-2xx код, тіло якого все одно треба розібрати (Steam пояснює в ньому причину).
    private async Task<T?> GetJsonAsync<T>(
        string url,
        CancellationToken ct,
        HttpStatusCode? nullOnStatus = null,
        HttpStatusCode? readBodyOnStatus = null) where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);

            if (nullOnStatus is not null && response.StatusCode == nullOnStatus)
                return null;

            if (readBodyOnStatus is null || response.StatusCode != readBodyOnStatus)
                response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new SteamApiException("Steam API недоступний або повернув помилку.", ex);
        }
        catch (JsonException ex)
        {
            throw new SteamApiException("Steam API повернув некоректну відповідь.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new SteamApiException("Steam API не відповів вчасно.", ex);
        }
    }

    private string SummariesUrl(IEnumerable<string> steamIds) =>
        $"{BaseUrl}/ISteamUser/GetPlayerSummaries/v2/"
        + $"?key={Uri.EscapeDataString(_apiKey)}"
        + $"&steamids={string.Join(",", steamIds.Select(Uri.EscapeDataString))}"
        + "&format=json";

    private string SummariesUrl(string steamId) => SummariesUrl(new[] { steamId });

    // 1 — приватний, 2 — лише для друзів, 3 — публічний
    private static ProfileVisibility ToVisibility(int state) => state switch
    {
        3 => ProfileVisibility.Public,
        2 => ProfileVisibility.Limited,
        _ => ProfileVisibility.Private
    };

    private static PlayerPresence ToPresence(PlayerSummaryDto p) => new(
        SteamId: p.SteamId!,
        PersonaName: p.PersonaName ?? string.Empty,
        AvatarUrl: p.AvatarFull ?? string.Empty,
        Visibility: ToVisibility(p.CommunityVisibilityState),
        PersonaState: p.PersonaState is { } s && Enum.IsDefined(typeof(PersonaState), s)
            ? (PersonaState)s
            : PersonaState.Offline,
        // Non-Steam гра має 64-бітний gameid, який не влазить в int → null, назва лишається
        CurrentGameId: int.TryParse(p.GameId, out var appId) ? appId : null,
        CurrentGameName: string.IsNullOrEmpty(p.GameExtraInfo) ? null : p.GameExtraInfo);

    private static OwnedGame ToDomain(OwnedGameDto d) => new(
        AppId: d.AppId,
        Name: d.Name ?? $"App {d.AppId}",
        PlaytimeForeverMinutes: d.PlaytimeForever,
        Playtime2WeeksMinutes: d.Playtime2Weeks,
        LastPlayed: d.RtimeLastPlayed > 0
            ? DateTimeOffset.FromUnixTimeSeconds(d.RtimeLastPlayed)
            : null,
        IconUrl: string.IsNullOrEmpty(d.ImgIconUrl)
            ? string.Empty
            : $"{IconBaseUrl}/{d.AppId}/{d.ImgIconUrl}.jpg",
        HasCommunityVisibleStats: d.HasCommunityVisibleStats ?? false);

    private static PlayerAchievement ToAchievement(PlayerAchievementDto a) => new(
        ApiName: a.ApiName!,
        DisplayName: string.IsNullOrEmpty(a.Name) ? a.ApiName! : a.Name,
        Description: string.IsNullOrEmpty(a.Description) ? null : a.Description,
        Achieved: a.Achieved != 0,
        UnlockTime: a.Achieved != 0 && a.UnlockTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(a.UnlockTime)
            : null);

    private static bool IsNoStatsError(string? error) =>
        error?.Contains("no stats", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsNotPublicError(string? error) =>
        error?.Contains("not public", StringComparison.OrdinalIgnoreCase) == true;
}
