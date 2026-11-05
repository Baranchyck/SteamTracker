using Microsoft.Data.Sqlite;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Models;

namespace SteamTracker.Tests.DataAccess;

public class DatabaseInitializerTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"steamtracker-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public async Task Initialize_OldUsersTable_AddsTimeCreatedAndKeepsRows()
    {
        // БД, створена версією до появи TimeCreated
        var factory = new SqliteConnectionFactory(_path);
        using (var connection = (SqliteConnection)factory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE Users (
                    SteamId TEXT PRIMARY KEY, PersonaName TEXT NOT NULL, AvatarUrl TEXT NOT NULL,
                    Visibility INTEGER NOT NULL, UpdatedAt TEXT NOT NULL);
                INSERT INTO Users VALUES ('1', 'Old', '', 1, '2026-01-01T00:00:00Z');
                """;
            command.ExecuteNonQuery();
        }

        new DatabaseInitializer(factory).Initialize();

        var repo = new SqliteUserRepository(factory);
        Assert.Equal(new SteamProfile("1", "Old", "", ProfileVisibility.Public), await repo.GetAsync("1"));

        var created = DateTimeOffset.FromUnixTimeSeconds(1063407589);
        await repo.UpsertAsync(new SteamProfile("1", "Old", "", ProfileVisibility.Public, created));
        Assert.Equal(created, (await repo.GetAsync("1"))!.TimeCreated);
    }

    [Fact]
    public void Initialize_Twice_DoesNotFail()
    {
        var factory = new SqliteConnectionFactory(_path);

        new DatabaseInitializer(factory).Initialize();
        new DatabaseInitializer(factory).Initialize();
    }
}
