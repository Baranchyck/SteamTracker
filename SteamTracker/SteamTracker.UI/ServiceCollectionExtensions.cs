using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Integrations.Auth;
using SteamTracker.Integrations.SteamApi;
using SteamTracker.Services.Auth;
using SteamTracker.Services.Configuration;
using SteamTracker.Services.Library;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Windows;

namespace SteamTracker.UI;

public static class ServiceCollectionExtensions
{
    public const string UseFakesKey = "UseFakes";

    public static IServiceCollection AddSteamTracker(
        this IServiceCollection services, IConfiguration configuration)
    {
        bool useFakes = configuration.GetValue<bool>(UseFakesKey);

        services.AddDatabase();
        if (!useFakes)
            services.AddSteamInfrastructure(configuration);

        // Кожен feature-PR додає/правит тільки свій рядок нижче.
        services.AddAuthFeature(useFakes, configuration);
        services.AddLibraryFeature(useFakes);

        services.AddWindows();
        return services;
    }

    // ---------- спільне ----------

    private static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddSingleton(_ => new SqliteConnectionFactory(SqliteConnectionFactory.GetDefaultPath()));
        services.AddSingleton<IDbConnectionFactory>(sp => sp.GetRequiredService<SqliteConnectionFactory>());
        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IUserRepository, SqliteUserRepository>();
        return services;
    }

    // Тільки для реального режиму: ключ потрібен лише тут.
    private static IServiceCollection AddSteamInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Load() валідує ключ і кидає ConfigurationMissingException ще на етапі реєстрації (fail-fast).
        services.AddSingleton(SteamOptions.Load(configuration));

        services.AddHttpClient<ISteamApiClient, SteamApiClient>((http, sp) =>
                new SteamApiClient(http, sp.GetRequiredService<SteamOptions>().ApiKey))
            // Typed client тримається singleton-сервісами всю роботу застосунку,
            // тож змушуємо пул з'єднань оновлюватись (DNS).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        return services;
    }

    // ---------- фічі ----------

    private static IServiceCollection AddAuthFeature(
        this IServiceCollection services, bool useFakes, IConfiguration configuration)
    {
        if (useFakes)
        {
            services.AddSingleton<IAuthService>(_ => new FakeAuthService
            {
                HasSavedSession = configuration.GetValue<bool>("Fakes:HasSavedSession"),
                SimulateFailure = configuration.GetValue<bool>("Fakes:SimulateFailure")
            });
            return services;
        }

        services.AddHttpClient<ISteamOpenIdAuthenticator, SteamOpenIdAuthenticator>();
        services.AddSingleton<IAuthService, AuthService>();
        return services;
    }

    private static IServiceCollection AddLibraryFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            services.AddSingleton<ILibraryService, FakeLibraryService>();
            return services;
        }

        services.AddSingleton<ILibraryService, LibraryService>();
        return services;
    }

    private static IServiceCollection AddWindows(this IServiceCollection services)
    {
        // Transient: після виходу й повторного входу потрібен новий екземпляр, закрите вікно WPF повторно не показати
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        // Нові вікна/екрани: services.AddTransient<SomeWindow>();
        return services;
    }
}
