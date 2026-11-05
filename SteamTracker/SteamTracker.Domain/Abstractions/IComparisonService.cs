using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// Порівняння часу гри з друзями (US 1.3). Список ігор для вибору — з ILibraryService.
public interface IComparisonService
{
    Task<ServiceResult<GameComparison>> GetComparisonAsync(int appId, CancellationToken ct = default);
}
