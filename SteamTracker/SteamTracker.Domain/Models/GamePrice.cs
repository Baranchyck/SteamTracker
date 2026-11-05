namespace SteamTracker.Domain.Models;

// Поточна ціна гри в Store (не ціна, за яку гру купили).
// FinalCents == null і IsFree == false — гра недоступна в регіоні.
public record GamePrice(
    int AppId,
    int? FinalCents,
    string? Currency,
    bool IsFree);
