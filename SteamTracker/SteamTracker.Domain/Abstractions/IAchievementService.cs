using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Achievement Planner — "Шлях до 100%" (US 1.2).
public interface IAchievementService
{
    // Ігри, де отримано хоча б одну, але не всі ачівки. Від найближчих до 100% — зверху.
    Task<ServiceResult<IReadOnlyList<AchievementGameProgress>>> GetGamesInProgressAsync(
        CancellationToken ct = default);

    // Недосягнуті ачівки гри, найлегші (з найбільшим глобальним %) — зверху.
    // Гра без ачівок — Available з порожнім планом.
    Task<ServiceResult<AchievementPlan>> GetPlanAsync(int appId, CancellationToken ct = default);

    // Без запитів у мережу: рахує по вже завантаженому плану.
    RoadmapResult BuildRoadmap(AchievementPlan plan, int targetPointsUp);
}
