using SteamTracker.Domain.Models;

namespace SteamTracker.Domain.Abstractions;

public interface IUserRepository { Task UpsertAsync(SteamProfile p); Task<SteamProfile?> GetAsync(string steamId); }
