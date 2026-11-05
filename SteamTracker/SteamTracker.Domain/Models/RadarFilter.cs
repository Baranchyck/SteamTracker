namespace SteamTracker.Domain.Models;

// OnlyAvailable — "вільні для гри": Online або LookingToPlay і зараз не в грі.
public record RadarFilter(bool OnlyAvailable = false);
