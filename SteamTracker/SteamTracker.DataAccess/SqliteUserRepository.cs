using System.Data.Common;
using System.Globalization;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;

namespace SteamTracker.DataAccess
{
    public sealed class SqliteUserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _factory;

        public SqliteUserRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task UpsertAsync(SteamProfile p)
        {
            ArgumentNullException.ThrowIfNull(p);

            using var connection = (DbConnection)_factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = """
                INSERT INTO Users (SteamId, PersonaName, AvatarUrl, Visibility, UpdatedAt)
                VALUES ($steamId, $personaName, $avatarUrl, $visibility, $updatedAt)
                ON CONFLICT (SteamId) DO UPDATE SET
                    PersonaName = excluded.PersonaName,
                    AvatarUrl   = excluded.AvatarUrl,
                    Visibility  = excluded.Visibility,
                    UpdatedAt   = excluded.UpdatedAt;
                """;
            command.AddParameter("$steamId", p.SteamId);
            command.AddParameter("$personaName", p.PersonaName);
            command.AddParameter("$avatarUrl", p.AvatarUrl);
            command.AddParameter("$visibility", (int)p.Visibility);
            command.AddParameter("$updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<SteamProfile?> GetAsync(string steamId)
        {
            using var connection = (DbConnection)_factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT SteamId, PersonaName, AvatarUrl, Visibility
                FROM Users
                WHERE SteamId = $steamId;
                """;
            command.AddParameter("$steamId", steamId);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new SteamProfile(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                (ProfileVisibility)reader.GetInt32(3));
        }
    }
}
