namespace SteamTracker.Domain.Abstractions;
public interface ISteamOpenIdAuthenticator { Task<string> AuthenticateAsync(CancellationToken ct = default); }
