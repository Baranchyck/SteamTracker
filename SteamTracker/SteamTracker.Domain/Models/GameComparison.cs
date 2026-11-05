namespace SteamTracker.Domain.Models;

public record GameComparison(
    int AppId,
    string GameName,
    // Від найбільшого часу до найменшого, користувач теж тут (IsCurrentUser)
    IReadOnlyList<ComparisonEntry> Ranking,
    int UserRank,
    // Час користувача у % від середнього (150 = в 1,5 раза більше).
    // Середнє — лише серед тих, у кого гра є і час > 0; null, якщо таких немає
    decimal? PercentOfAverage,
    IReadOnlyList<SteamProfile> FriendsWithoutGame,
    // Бібліотека прихована — невідомо, чи є гра, тому окремо, а не "0 годин"
    IReadOnlyList<SteamProfile> FriendsWithPrivateLibrary);
