using Microsoft.Data.Sqlite;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Models;

namespace SteamTracker.Tests.DataAccess;

public class SqliteUserRepositoryTests : IDisposable
{
    private readonly TempSqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Upsert_ThenGet_ReturnsSameProfile()
    {
        var repo = new SqliteUserRepository(_db.CreateFactory());
        var profile = new SteamProfile("76561197960287930", "Gabe", "https://avatars/gabe.jpg", ProfileVisibility.Public);

        await repo.UpsertAsync(profile);
        var loaded = await repo.GetAsync(profile.SteamId);

        Assert.Equal(profile, loaded);
    }

    [Fact]
    public async Task Upsert_Twice_UpdatesRowWithoutDuplicate()
    {
        var factory = _db.CreateFactory();
        var repo = new SqliteUserRepository(factory);

        await repo.UpsertAsync(new SteamProfile("1", "Old", "https://a/old.jpg", ProfileVisibility.Private));
        await repo.UpsertAsync(new SteamProfile("1", "New", "https://a/new.jpg", ProfileVisibility.Public));

        var loaded = await repo.GetAsync("1");
        Assert.Equal(new SteamProfile("1", "New", "https://a/new.jpg", ProfileVisibility.Public), loaded);

        using var connection = (SqliteConnection)factory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE SteamId = '1';";
        Assert.Equal(1L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNull()
    {
        var repo = new SqliteUserRepository(_db.CreateFactory());

        Assert.Null(await repo.GetAsync("does-not-exist"));
    }

    [Fact]
    public async Task CurrentUser_SurvivesRestart()
    {
        var profile = new SteamProfile("76561197960287930", "Gabe", "https://avatars/gabe.jpg", ProfileVisibility.Limited);

        var firstRunFactory = _db.CreateFactory();
        await new SqliteUserRepository(firstRunFactory).UpsertAsync(profile);
        await new SqliteSettingsRepository(firstRunFactory).SetAsync(SettingsKeys.CurrentSteamId, profile.SteamId);
        SqliteConnection.ClearAllPools();

        var secondRunFactory = _db.CreateFactory();
        new DatabaseInitializer(secondRunFactory).Initialize();
        var steamId = await new SqliteSettingsRepository(secondRunFactory).GetAsync(SettingsKeys.CurrentSteamId);
        var loaded = await new SqliteUserRepository(secondRunFactory).GetAsync(steamId!);

        Assert.Equal(profile.SteamId, steamId);
        Assert.Equal(profile, loaded);
    }
}
