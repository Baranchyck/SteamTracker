using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.SteamApi.Dtos;

internal class GetOwnedGamesEnvelope
{
    [JsonPropertyName("response")]
    public GetOwnedGamesResponse? Response { get; set; }
}

internal class GetOwnedGamesResponse
{
    [JsonPropertyName("game_count")]
    public int? GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<OwnedGameDto>? Games { get; set; }
}

internal class OwnedGameDto
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("playtime_2weeks")]
    public int Playtime2Weeks { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string? ImgIconUrl { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long RtimeLastPlayed { get; set; }
}