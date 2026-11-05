namespace SteamTracker.Domain.Models;

public record AchievementGameProgress(
    int AppId,
    string Name,
    string IconUrl,
    int Achieved,
    int Total)
{
    // 0..100; гра без ачівок — 0
    public decimal Percent => AchievementMath.Percent(Achieved, Total);
}
