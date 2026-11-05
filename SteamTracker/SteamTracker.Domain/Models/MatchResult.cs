namespace SteamTracker.Domain.Models;

public record MatchResult(
    // Поточний користувач — першим, далі в порядку вводу
    IReadOnlyList<MatchParticipant> Participants,
    // Від найбільшого середнього часу гри до найменшого
    IReadOnlyList<CommonGame> CommonGames,
    // Вводи, які не вдалося перетворити на профіль, з причиною
    IReadOnlyList<SteamIdResolveResult> InvalidInputs,
    // Учасники з прихованою бібліотекою (підмножина Participants) — показати окремо з поясненням
    IReadOnlyList<MatchParticipant> HiddenLibraryParticipants);
