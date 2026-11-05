using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.Store.Dtos;

// Відповідь appdetails — словник "appid" -> AppDetailsEntry.
internal class AppDetailsEntry
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    // З filters=price_overview: об'єкт { "price_overview": {...} } для платної гри,
    // але порожній масив [] для безкоштовної, тому JsonElement, а не клас.
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
