namespace ﻿SteamTracker.Domain.Models;

// TimeCreated — дата реєстрації акаунту; null, якщо Steam її не віддав (приватний профіль).
// Останній і з дефолтом, щоб не ламати існуючі місця створення.
public record SteamProfile(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    ProfileVisibility Visibility,
    DateTimeOffset? TimeCreated = null);
