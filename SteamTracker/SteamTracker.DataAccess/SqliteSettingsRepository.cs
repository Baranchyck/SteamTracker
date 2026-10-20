using System.Data.Common;
using SteamTracker.Domain.Abstractions;

namespace SteamTracker.DataAccess
{
    public sealed class SqliteSettingsRepository : ISettingsRepository
    {
        private readonly IDbConnectionFactory _factory;

        public SqliteSettingsRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<string?> GetAsync(string key)
        {
            using var connection = (DbConnection)_factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            command.AddParameter("$key", key);

            return await command.ExecuteScalarAsync() as string;
        }

        public async Task SetAsync(string key, string value)
        {
            using var connection = (DbConnection)_factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = """
                INSERT INTO Settings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
                """;
            command.AddParameter("$key", key);
            command.AddParameter("$value", value);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(string key)
        {
            using var connection = (DbConnection)_factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = "DELETE FROM Settings WHERE Key = $key;";
            command.AddParameter("$key", key);

            await command.ExecuteNonQueryAsync();
        }
    }
}
