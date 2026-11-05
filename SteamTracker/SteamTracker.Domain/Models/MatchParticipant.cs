namespace SteamTracker.Domain.Models;

public record MatchParticipant(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    PersonaState PersonaState,
    bool IsCurrentUser,
    // Бібліотека прихована — учасник не бере участі в перетині
    bool LibraryHidden);
