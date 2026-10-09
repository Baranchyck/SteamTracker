using SteamTracker.Services.Models;

namespace SteamTracker.Services.Abstractions;

public interface IUserRepository { Task UpsertAsync(SteamProfile p); Task<SteamProfile?> GetAsync(string steamId); }
