using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Co-op Matchmaker: спільні ігри групи (US 1.5).
public interface IMatchmakerService
{
    // Найменше / найбільше учасників разом з поточним користувачем
    public const int MinParticipants = 2;
    public const int MaxParticipants = 8;

    // participantInputs — те, що ввів користувач (SteamID64, посилання, нік); себе вводити не треба.
    // Невалідні вводи не валять запит — вони в MatchResult.InvalidInputs.
    Task<ServiceResult<MatchResult>> FindCommonGamesAsync(
        IReadOnlyList<string> participantInputs, CancellationToken ct = default);
}
