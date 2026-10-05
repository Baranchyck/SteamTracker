using System.Data;
using Microsoft.Data.Sqlite;

namespace SteamTracker.DataAccess
{
    public sealed class SqliteConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(string databasePath)
        {
            string? dir = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }

        public IDbConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public static string GetDefaultPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "SteamTracker", "steamtracker.db");
        }
    }
}