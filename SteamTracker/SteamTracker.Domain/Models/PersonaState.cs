namespace SteamTracker.Domain.Models;

// Значення збігаються з полем personastate у GetPlayerSummaries
public enum PersonaState
{
    Offline = 0,
    Online = 1,
    Busy = 2,
    Away = 3,
    Snooze = 4,
    LookingToTrade = 5,
    LookingToPlay = 6
}
