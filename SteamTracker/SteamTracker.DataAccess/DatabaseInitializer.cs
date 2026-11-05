using System.Data;
using System.Reflection;

namespace SteamTracker.DataAccess
{
    public sealed class DatabaseInitializer
    {
        private const string SchemaResourceName = "schema.sql";

        private readonly IDbConnectionFactory _factory;

        public DatabaseInitializer(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public void Initialize()
        {
            string sql = ReadSchema();

            using var connection = _factory.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = sql;
            command.ExecuteNonQuery();

            // CREATE TABLE IF NOT EXISTS не чіпає таблиці з БД, створеної старішою версією,
            // тож колонки, додані пізніше, доводимо окремо
            EnsureColumn(connection, "Users", "TimeCreated", "INTEGER");
        }

        // Назви таблиць і колонок — константи з коду, не ввід користувача
        private static void EnsureColumn(IDbConnection connection, string table, string column, string type)
        {
            using var check = connection.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
            if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                return;

            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type};";
            alter.ExecuteNonQuery();
        }

        private static string ReadSchema()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
                ?? throw new InvalidOperationException(
                    $"Ресурс '{SchemaResourceName}' не знайдено в збірці. " +
                    "Перевір EmbeddedResource і LogicalName у SteamTracker.DataAccess.csproj.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}