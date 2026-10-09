using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

public interface ILibraryService
{
    Task<ServiceResult<IReadOnlyList<OwnedGame>>> GetMyLibraryAsync(LibraryFilter filter, CancellationToken ct = default);
}