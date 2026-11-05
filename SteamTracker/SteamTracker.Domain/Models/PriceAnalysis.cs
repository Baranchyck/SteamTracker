namespace SteamTracker.Domain.Models;

public record PriceAnalysis(
    IReadOnlyList<PricePerHourRow> Games,
    // Найменша ціна/год серед ігор, у які грали хоча б годину
    IReadOnlyList<PricePerHourRow> BestValue,
    // Ціна вища за медіану, а грали менше 2 годин (сюди ж ігри з 0 хвилин)
    IReadOnlyList<PricePerHourRow> ExpensiveBarelyPlayed,
    // Примітка для екрана: ціна поточна, а не та, за яку гру купили
    string Note);
