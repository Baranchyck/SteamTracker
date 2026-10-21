using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamTracker.UI.Fakes
{
    /// <summary>
    /// Читає тестові дані для фейкових сервісів з JSON-файлів у Fakes/Data (копіюються поруч із .exe).
    /// </summary>
    internal static class FakeData
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static async Task<T> LoadAsync<T>(string fileName, CancellationToken ct)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fakes", "Data", fileName);

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct)
                ?? throw new InvalidDataException($"Fake data file '{fileName}' is empty.");
        }
    }
}
