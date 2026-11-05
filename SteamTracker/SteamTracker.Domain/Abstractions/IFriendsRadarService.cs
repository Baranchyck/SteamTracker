using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Друзі-радар: хто з друзів онлайн і у що грає (US 1.4).
public interface IFriendsRadarService
{
    // Без кешу: статуси міняються щохвилини, "Оновити" — це просто новий виклик.
    // Порядок: в грі → онлайн → відійшов/зайнятий → офлайн, всередині групи за ніком.
    // Список друзів прихований — Private; друзів немає — Available з порожнім списком.
    Task<ServiceResult<IReadOnlyList<PlayerPresence>>> GetFriendsAsync(
        RadarFilter filter, CancellationToken ct = default);
}
