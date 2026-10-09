namespace SteamTracker.Services.Models;

public record OwnedGame(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    int Playtime2WeeksMinutes,
    DateTimeOffset? LastPlayed,
    string IconUrl);