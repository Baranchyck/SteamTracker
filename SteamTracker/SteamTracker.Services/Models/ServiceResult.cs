namespace SteamTracker.Services.Models;

public record ServiceResult<T>(T? Value, DataStatus Status, string? Message = null);