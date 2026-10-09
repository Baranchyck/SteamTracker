namespace SteamTracker.Services.Abstractions;
public interface ISteamOpenIdAuthenticator { Task<string> AuthenticateAsync(CancellationToken ct = default); }
