namespace SteamTracker.Domain.Models;

// HasCommunityVisibleStats — у гри є статистика/ачівки (кандидат для Achievement Planner).
// Останній і з дефолтом, щоб не ламати існуючі місця створення.
public record OwnedGame(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    int Playtime2WeeksMinutes,
    DateTimeOffset? LastPlayed,
    string IconUrl,
    bool HasCommunityVisibleStats = false);
