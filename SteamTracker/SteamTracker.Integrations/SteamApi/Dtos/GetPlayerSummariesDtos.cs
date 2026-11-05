using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.SteamApi.Dtos;

internal class GetPlayerSummariesEnvelope
{
    [JsonPropertyName("response")]
    public GetPlayerSummariesResponse? Response { get; set; }
}

internal class GetPlayerSummariesResponse
{
    [JsonPropertyName("players")]
    public List<PlayerSummaryDto>? Players { get; set; }
}

internal class PlayerSummaryDto
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("personaname")]
    public string? PersonaName { get; set; }

    [JsonPropertyName("avatarfull")]
    public string? AvatarFull { get; set; }

    // 1 — приватний, 2 — лише для друзів, 3 — публічний
    [JsonPropertyName("communityvisibilitystate")]
    public int CommunityVisibilityState { get; set; }

    // Для приватних профілів Steam не віддає поле взагалі
    [JsonPropertyName("personastate")]
    public int? PersonaState { get; set; }

    // Поточна гра: є тільки коли гравець у грі. gameid приходить рядком;
    // для non-Steam ігор це 64-бітне число, а не AppId.
    [JsonPropertyName("gameid")]
    public string? GameId { get; set; }

    [JsonPropertyName("gameextrainfo")]
    public string? GameExtraInfo { get; set; }
}
