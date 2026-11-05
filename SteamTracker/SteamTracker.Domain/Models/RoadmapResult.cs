namespace SteamTracker.Domain.Models;

public record RoadmapResult(
    // Скільки пунктів % просили додати
    int TargetPointsUp,
    // Які ачівки закрити (k найлегших з плану)
    IReadOnlyList<PlannedAchievement> Achievements,
    decimal CurrentPercent,
    // % після закриття всіх Achievements; може бути меншим за Current + Target,
    // якщо недосягнутих не вистачає (тоді це 100)
    decimal NewPercent);
