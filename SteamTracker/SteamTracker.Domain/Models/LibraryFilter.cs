namespace SteamTracker.Domain.Models;

public record LibraryFilter(bool OnlyNeverPlayed = false, LibrarySort Sort = LibrarySort.NameAsc);