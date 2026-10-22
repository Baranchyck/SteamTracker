using System.Net.Http.Json;
using System.Text.Json;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.SteamApi.Dtos;

namespace SteamTracker.Integrations.SteamApi;

public class SteamApiClient : ISteamApiClient
{
    private const string IconBaseUrl =
        "https://media.steampowered.com/steamcommunity/public/images/apps";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public SteamApiClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(
        string steamId, CancellationToken ct = default)
    {
        var url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/"
                  + $"?key={Uri.EscapeDataString(_apiKey)}"
                  + $"&steamid={Uri.EscapeDataString(steamId)}"
                  + "&include_appinfo=true&include_played_free_games=true&format=json";

        GetOwnedGamesEnvelope? envelope;
        try
        {
            envelope = await _httpClient.GetFromJsonAsync<GetOwnedGamesEnvelope>(url, ct);
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

        // Закритий профіль: "response": {} без game_count і games.
        // TODO(#нова-таска): тут має бути null, а не порожній список
        var games = envelope?.Response?.Games;
        if (envelope?.Response?.GameCount is null || games is null)
            return Array.Empty<OwnedGame>();

        return games.Select(ToDomain).ToList();
    }

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
            : $"{IconBaseUrl}/{d.AppId}/{d.ImgIconUrl}.jpg");
}