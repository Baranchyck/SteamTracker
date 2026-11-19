using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SteamTracker.UI.Logging;

namespace SteamTracker.Tests.Logging;

public class FileLoggerProviderTests : IDisposable
{
    private const string ApiKey = "ABCDEF0123456789ABCDEF0123456789";

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"steamtracker-logs-{Guid.NewGuid():N}");

    private DateTimeOffset _now = new(2026, 11, 15, 18, 0, 0, TimeSpan.FromHours(2));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private FileLoggerProvider CreateProvider(params string?[] secrets) => new(_dir, secrets, () => _now);

    private string ReadLog(FileLoggerProvider provider, DateOnly date)
    {
        // FileShare.ReadWrite — так само, як відкриває провайдер; файл можна читати, поки він відкритий
        using var stream = new FileStream(provider.GetFilePath(date), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        using var provider = CreateProvider();

        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void Log_Information_WritesLevelShortCategoryAndMessage()
    {
        using var provider = CreateProvider();
        var logger = provider.CreateLogger("SteamTracker.Services.Library.LibraryService");

        logger.LogInformation("Старт {Value}", 42);

        var text = ReadLog(provider, new DateOnly(2026, 11, 15));
        Assert.Contains("[INF] LibraryService: Старт 42", text);
        Assert.StartsWith("2026-11-15 18:00:00.000 +02:00", text);
    }

    [Fact]
    public void Log_Error_WritesFullExceptionWithInnerAndStackTrace()
    {
        using var provider = CreateProvider();
        var logger = provider.CreateLogger("App");

        var error = Assert.ThrowsAny<Exception>(ThrowNested);

        logger.LogError(error, "Не вдалося ініціалізувати базу даних");

        var text = ReadLog(provider, new DateOnly(2026, 11, 15));
        Assert.Contains("[ERR] App: Не вдалося ініціалізувати базу даних", text);
        Assert.Contains("System.InvalidOperationException: Ресурс 'schema.sql' не знайдено", text);
        Assert.Contains("System.IO.FileNotFoundException: inner", text);
        Assert.Contains(nameof(ThrowNested), text); // стек
    }

    [Fact]
    public void Log_MessageWithApiKey_KeyIsMasked()
    {
        using var provider = CreateProvider(ApiKey);
        var logger = provider.CreateLogger("SteamApiClient");

        logger.LogWarning(new HttpRequestException($"?key={ApiKey}&steamid=1"), "Запит з ключем {Key}", ApiKey);

        var text = ReadLog(provider, new DateOnly(2026, 11, 15));
        Assert.DoesNotContain(ApiKey, text);
        Assert.Contains(LogRedactor.Mask, text);
    }

    [Fact]
    public void Log_DateChanges_WritesToNewDailyFile()
    {
        using var provider = CreateProvider();
        var logger = provider.CreateLogger("App");

        logger.LogInformation("перший день");
        _now = _now.AddDays(1);
        logger.LogInformation("другий день");

        var first = ReadLog(provider, new DateOnly(2026, 11, 15));
        var second = ReadLog(provider, new DateOnly(2026, 11, 16));
        Assert.Contains("перший день", first);
        Assert.DoesNotContain("другий день", first);
        Assert.Contains("другий день", second);
    }

    [Fact]
    public void Log_AfterDispose_DoesNotThrow()
    {
        var provider = CreateProvider();
        var logger = provider.CreateLogger("App");
        provider.Dispose();

        var ex = Record.Exception(() => logger.LogError("після Dispose"));

        Assert.Null(ex);
    }

    [Fact]
    public void LoggingSetup_Factory_FiltersBelowInformationAndMasksConfiguredKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Steam:ApiKey"] = ApiKey })
            .Build();

        using (var factory = LoggingSetup.CreateLoggerFactory(configuration, _dir))
        {
            var logger = factory.CreateLogger("App");
            logger.LogDebug("debug-не-має-бути");
            logger.LogInformation("ключ {Key}", ApiKey);
            factory.CreateLogger("System.Net.Http.HttpClient.Default").LogInformation("http-шум");
        }

        var file = Directory.GetFiles(_dir, $"{FileLoggerProvider.FilePrefix}*.log").Single();
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("debug-не-має-бути", text);
        Assert.DoesNotContain("http-шум", text);
        Assert.DoesNotContain(ApiKey, text);
        Assert.Contains($"ключ {LogRedactor.Mask}", text);
    }

    private static void ThrowNested()
    {
        try
        {
            throw new FileNotFoundException("inner");
        }
        catch (FileNotFoundException inner)
        {
            throw new InvalidOperationException("Ресурс 'schema.sql' не знайдено в збірці.", inner);
        }
    }
}
