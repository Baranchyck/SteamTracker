using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.SteamApi.Dtos;

internal class ResolveVanityUrlEnvelope
{
    [JsonPropertyName("response")]
    public ResolveVanityUrlResponse? Response { get; set; }
}

internal class ResolveVanityUrlResponse
{
    // 1 — знайдено, 42 — немає такого ніку
    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("steamid")]
    public string? SteamId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
