using SteamTracker.DataAccess;

namespace SteamTracker.Tests.DataAccess;

public class SqliteSettingsRepositoryTests : IDisposable
{
    private readonly TempSqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var repo = new SqliteSettingsRepository(_db.CreateFactory());

        await repo.SetAsync("Theme", "Dark");

        Assert.Equal("Dark", await repo.GetAsync("Theme"));
    }

    [Fact]
    public async Task Set_Twice_OverwritesValue()
    {
        var repo = new SqliteSettingsRepository(_db.CreateFactory());

        await repo.SetAsync("Theme", "Dark");
        await repo.SetAsync("Theme", "Light");

        Assert.Equal("Light", await repo.GetAsync("Theme"));
    }

    [Fact]
    public async Task Delete_RemovesValue()
    {
        var repo = new SqliteSettingsRepository(_db.CreateFactory());

        await repo.SetAsync("Theme", "Dark");
        await repo.DeleteAsync("Theme");

        Assert.Null(await repo.GetAsync("Theme"));
    }

    [Fact]
    public async Task Get_Missing_ReturnsNull()
    {
        var repo = new SqliteSettingsRepository(_db.CreateFactory());

        Assert.Null(await repo.GetAsync("missing"));
    }
}
