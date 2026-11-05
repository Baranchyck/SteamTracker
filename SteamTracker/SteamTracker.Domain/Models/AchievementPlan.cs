namespace SteamTracker.Domain.Models;

public record AchievementPlan(
    int AppId,
    int Achieved,
    int Total,
    // Тільки недосягнуті, від найлегших до найважчих; без глобального % — у кінці
    IReadOnlyList<PlannedAchievement> Remaining)
{
    public decimal Percent => AchievementMath.Percent(Achieved, Total);
}
