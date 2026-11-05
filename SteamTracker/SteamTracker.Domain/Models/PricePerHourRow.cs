namespace SteamTracker.Domain.Models;

// Ціни в центах, як у GamePrice: для показу ділити на 100.
public record PricePerHourRow(
    int AppId,
    string Name,
    int PlaytimeMinutes,
    int? PriceCents,
    string? Currency,
    bool IsFree,
    // null — гра безкоштовна, ціна невідома або 0 хвилин гри (ділити нема на що)
    decimal? PricePerHourCents,
    // Ціну не вдалося отримати: Store недоступний або гра не продається в регіоні
    bool PriceUnavailable);
