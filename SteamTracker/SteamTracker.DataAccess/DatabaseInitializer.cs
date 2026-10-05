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