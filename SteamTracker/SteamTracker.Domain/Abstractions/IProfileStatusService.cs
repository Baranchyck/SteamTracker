using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Поточний статус користувача, що увійшов, для картки в бічному меню (#133): у грі / онлайн / відійшов / офлайн.
// Без кешу: статус міняється щохвилини, оновлення — просто новий виклик.
// null — статус отримати не вдалося (немає входу, Steam недоступний або не віддав профіль).
public interface IProfileStatusService
{
    Task<PlayerPresence?> GetCurrentStatusAsync(CancellationToken ct = default);
}
