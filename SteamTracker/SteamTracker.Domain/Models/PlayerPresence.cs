namespace SteamTracker.Domain.Models;

// Профіль + поточна активність гравця (для друзів-радару, порівняння, матчмейкера).
// CurrentGameId/CurrentGameName — null, якщо гравець не в грі, офлайн або профіль приватний.
// Для сторонніх (non-Steam) ігор Steam віддає не-AppId ідентифікатор: тоді CurrentGameId = null,
// а назва гри все одно є в CurrentGameName.
public record PlayerPresence(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    ProfileVisibility Visibility,
    PersonaState PersonaState,
    int? CurrentGameId,
    string? CurrentGameName);
