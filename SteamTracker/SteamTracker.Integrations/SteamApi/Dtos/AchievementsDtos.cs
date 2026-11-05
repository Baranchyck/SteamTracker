using System.Text.Json.Serialization;

namespace SteamTracker.Integrations.SteamApi.Dtos;

// ---------- ISteamUserStats/GetPlayerAchievements/v1 ----------

internal class GetPlayerAchievementsEnvelope
{
    [JsonPropertyName("playerstats")]
    public PlayerStatsDto? PlayerStats { get; set; }
}

internal class PlayerStatsDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    // Напр. "Requested app has no stats", "Profile is not public"
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    // Відсутнє, якщо у гри є статистика, але немає ачівок
    [JsonPropertyName("achievements")]
    public List<PlayerAchievementDto>? Achievements { get; set; }
}

internal class PlayerAchievementDto
{
    [JsonPropertyName("apiname")]
    public string? ApiName { get; set; }

    // 0 / 1
    [JsonPropertyName("achieved")]
    public int Achieved { get; set; }

    // Unix-час; 0 для неотриманих
    [JsonPropertyName("unlocktime")]
    public long UnlockTime { get; set; }

    // name/description приходять лише з параметром l=<мова>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

// ---------- ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2 ----------

internal class GlobalPercentagesEnvelope
{
    [JsonPropertyName("achievementpercentages")]
    public GlobalPercentagesResponse? AchievementPercentages { get; set; }
}

internal class GlobalPercentagesResponse
{
    [JsonPropertyName("achievements")]
    public List<GlobalPercentageDto>? Achievements { get; set; }
}

internal class GlobalPercentageDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // Steam віддає то числом (12.3), то рядком ("12.3")
    [JsonPropertyName("percent")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double Percent { get; set; }
}
