namespace SteamTracker.Domain.Models;

public record CommonGame(
    int AppId,
    string Name,
    string IconUrl,
    // Середнє серед учасників з відкритою бібліотекою; проксі "популярності в компанії"
    decimal AveragePlaytimeMinutes,
    // Ключ — SteamID учасника
    IReadOnlyDictionary<string, int> PlaytimeMinutesBySteamId);
