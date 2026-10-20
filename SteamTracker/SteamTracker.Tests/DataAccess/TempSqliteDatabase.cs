using Microsoft.Data.Sqlite;
using SteamTracker.DataAccess;

namespace SteamTracker.Tests.DataAccess;

// Тимчасовий файл БД: фабрика відкриває нове з'єднання на кожен виклик,
// тому in-memory БД зникала б між викликами репозиторію.
public sealed class TempSqliteDatabase : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"steamtracker-test-{Guid.NewGuid():N}.db");

    public TempSqliteDatabase()
    {
        new DatabaseInitializer(CreateFactory()).Initialize();
    }

    // Нова фабрика на той самий файл — імітує перезапуск застосунку.
    public SqliteConnectionFactory CreateFactory() => new(Path);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Path))
            File.Delete(Path);
    }
}
