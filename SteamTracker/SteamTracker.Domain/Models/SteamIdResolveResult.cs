namespace SteamTracker.Domain.Models;

// Успіх — SteamId заповнений; помилка — Error і Message (текст для користувача).
public record SteamIdResolveResult(
    string Input,
    string? SteamId,
    SteamIdResolveError? Error,
    string? Message)
{
    public bool IsSuccess => SteamId is not null;

    public static SteamIdResolveResult Ok(string input, string steamId) => new(input, steamId, null, null);

    public static SteamIdResolveResult Fail(string input, SteamIdResolveError error, string message) =>
        new(input, null, error, message);
}
