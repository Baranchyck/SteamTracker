using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

// "Ціна за годину": скільки коштує година гри в кожній грі бібліотеки.
public interface IPriceAnalyzerService
{
    // Store недоступний — це не Error: Available, рядки з PriceUnavailable і пояснення в Message.
    Task<ServiceResult<PriceAnalysis>> GetAnalysisAsync(CancellationToken ct = default);
}
