namespace SteamTracker.Domain.Models;

public record ServiceResult<T>(T? Value, DataStatus Status, string? Message = null);