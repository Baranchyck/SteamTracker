using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SteamTracker.Services.Configuration;

namespace SteamTracker.UI.Logging;

public static class LoggingSetup
{
    // Фабрику створюємо ДО збирання DI-контейнера: інакше помилку самого контейнера нічим залогувати
    public static ILoggerFactory CreateLoggerFactory(IConfiguration configuration, string? directory = null)
    {
        // Ключ Steam передаємо провайдеру як секрет — він замаскує його в будь-якому повідомленні
        var apiKey = configuration[$"{SteamOptions.SectionName}:{nameof(SteamOptions.ApiKey)}"];
        var provider = new FileLoggerProvider(directory ?? FileLoggerProvider.GetDefaultDirectory(), new[] { apiKey });

        var filters = new LoggerFilterOptions { MinLevel = LogLevel.Information };
        // HttpClientFactory на Information пише кожен запит разом з URL — для нас це шум
        filters.Rules.Add(new LoggerFilterRule(null, "System.Net.Http", LogLevel.Warning, null));

        var factory = new LoggerFactory(Array.Empty<ILoggerProvider>(), filters);
        // Через AddProvider фабрика "володіє" провайдером: Dispose фабрики закриє і файл логу
        factory.AddProvider(provider);
        return factory;
    }

    // Та сама фабрика, що й на старті, — один файл і один провайдер на весь застосунок.
    // Реєструємо як готовий екземпляр: DI його не створював і не диспозить, це робить App.OnExit.
    public static IServiceCollection AddAppLogging(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }
}
