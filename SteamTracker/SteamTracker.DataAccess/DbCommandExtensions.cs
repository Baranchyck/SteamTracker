using System.Data;

namespace SteamTracker.DataAccess
{
    internal static class DbCommandExtensions
    {
        public static void AddParameter(this IDbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
