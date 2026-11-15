using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SteamTracker.UI.Logging;

// Мінімальний файловий провайдер для Microsoft.Extensions.Logging: один файл на день,
// %LocalAppData%\SteamTracker\logs\steamtracker-yyyyMMdd.log (поза репозиторієм).
// Застосунок локальний і однопроцесний, тож синхронний запис під lock простіший і надійніший
// за фонову чергу: після падіння в файлі гарантовано є все, що встигли залогувати.
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const string FilePrefix = "steamtracker-";

    private readonly string _directory;
    private readonly LogRedactor _redactor;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _sync = new();

    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _disposed;

    public FileLoggerProvider(string directory, IEnumerable<string?>? secrets = null, Func<DateTimeOffset>? clock = null)
    {
        _directory = directory;
        _redactor = new LogRedactor(secrets ?? Array.Empty<string?>());
        _clock = clock ?? (() => DateTimeOffset.Now);
        Directory.CreateDirectory(_directory);
    }

    public static string GetDefaultDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SteamTracker", "logs");
    }

    public string GetFilePath(DateOnly date) =>
        Path.Combine(_directory, $"{FilePrefix}{date:yyyyMMdd}.log");

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, ShortCategory(categoryName));

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _clock();

        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(LevelTag(level)).Append("] ")
            .Append(category).Append(": ")
            .Append(message);

        // ToString() винятку — тип, повідомлення, стек і всі InnerException
        if (exception is not null)
            line.AppendLine().Append(exception);

        var text = _redactor.Redact(line.ToString());

        lock (_sync)
        {
            if (_disposed)
                return;

            try
            {
                GetWriter(DateOnly.FromDateTime(now.DateTime)).WriteLine(text);
            }
            catch (IOException)
            {
                // Лог не має валити застосунок: диск заповнений або файл заблоковано — просто пропускаємо запис
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // Новий файл при зміні дати; FileShare.ReadWrite — щоб лог можна було відкрити, поки застосунок працює
    private StreamWriter GetWriter(DateOnly date)
    {
        if (_writer is not null && _writerDate == date)
            return _writer;

        _writer?.Dispose();
        var stream = new FileStream(GetFilePath(date), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        _writerDate = date;
        return _writer;
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    // SteamTracker.Services.Library.LibraryService -> LibraryService: коротше й читабельніше в рядку логу
    private static string ShortCategory(string category)
    {
        int dot = category.LastIndexOf('.');
        return dot >= 0 && dot < category.Length - 1 ? category[(dot + 1)..] : category;
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Мінімальний рівень фільтрує LoggerFactory (SetMinimumLevel), тут лише відсікаємо None
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            _provider.Write(logLevel, _category, formatter(state, exception), exception);
        }
    }
}
