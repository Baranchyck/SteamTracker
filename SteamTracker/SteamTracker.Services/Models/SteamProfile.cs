using SteamTracker.Services.Models;

public record SteamProfile(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    ProfileVisibility Visibility);