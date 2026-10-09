using SteamTracker.Services.Models;

namespace SteamTracker.Services.Abstractions;

public interface ILibraryService
{
    Task<ServiceResult<IReadOnlyList<OwnedGame>>> GetMyLibraryAsync(LibraryFilter filter, CancellationToken ct = default);
}