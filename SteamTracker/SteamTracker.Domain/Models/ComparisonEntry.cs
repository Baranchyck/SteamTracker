namespace SteamTracker.Domain.Models;

// Рядок рейтингу: користувач або друг, у якого гра є.
public record ComparisonEntry(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    int PlaytimeForeverMinutes,
    int Playtime2WeeksMinutes,
    // Місце з 1; однаковий час — однакове місце (1, 2, 2, 4)
    int Rank,
    bool IsCurrentUser);
