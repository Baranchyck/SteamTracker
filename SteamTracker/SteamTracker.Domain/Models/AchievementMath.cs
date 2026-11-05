namespace SteamTracker.Domain.Models;

internal static class AchievementMath
{
    // Округлення до 2 знаків, щоб на екрані не було 33.333333…
    public static decimal Percent(int achieved, int total) =>
        total <= 0 ? 0m : Math.Round(achieved * 100m / total, 2);
}
