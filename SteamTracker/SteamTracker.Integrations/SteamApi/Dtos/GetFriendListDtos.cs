using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.SteamApi.Dtos;

internal class GetFriendListEnvelope
{
    [JsonPropertyName("friendslist")]
    public GetFriendListResponse? FriendsList { get; set; }
}

internal class GetFriendListResponse
{
    [JsonPropertyName("friends")]
    public List<FriendDto>? Friends { get; set; }
}

internal class FriendDto
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }
}
