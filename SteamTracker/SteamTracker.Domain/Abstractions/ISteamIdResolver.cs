using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Перетворює те, що ввів користувач (SteamID64, посилання на профіль, vanity-нік), у SteamID64.
// Не кидає винятків на поганий ввід чи збій Steam — причина в результаті.
public interface ISteamIdResolver
{
    Task<SteamIdResolveResult> ResolveAsync(string input, CancellationToken ct = default);
}
