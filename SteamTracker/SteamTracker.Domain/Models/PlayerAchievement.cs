namespace SteamTracker.Domain.Models;

// ApiName — технічний ключ ачівки (той самий, що в глобальних відсотках).
// DisplayName/Description — локалізовані Steam; Description буває відсутнім (приховані ачівки).
// UnlockTime — тільки для отриманих.
public record PlayerAchievement(
    string ApiName,
    string DisplayName,
    string? Description,
    bool Achieved,
    DateTimeOffset? UnlockTime);
