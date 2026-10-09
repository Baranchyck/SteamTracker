namespace SteamTracker.Services.Abstractions;

public interface ISettingsRepository { Task<string?> GetAsync(string key); Task SetAsync(string key, string value); Task DeleteAsync(string key); }
