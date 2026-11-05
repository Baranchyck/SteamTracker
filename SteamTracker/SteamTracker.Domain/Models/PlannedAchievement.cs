namespace SteamTracker.Domain.Models;

// Недосягнута ачівка в плані.
// GlobalPercent — частка гравців, які її отримали (0..100);
// null — Steam не віддав глобальну статистику для неї (або вона зараз недоступна).
public record PlannedAchievement(
    string ApiName,
    string DisplayName,
    string? Description,
    double? GlobalPercent);
