using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Integrations.Auth;
using SteamTracker.Integrations.SteamApi;
using SteamTracker.Integrations.Store;
using SteamTracker.Services.Achievements;
using SteamTracker.Services.Auth;
using SteamTracker.Services.Comparison;
using SteamTracker.Services.Configuration;
using SteamTracker.Services.FriendsRadar;
using SteamTracker.Services.Library;
using SteamTracker.Services.Matchmaker;
using SteamTracker.Services.PricePerHour;
using SteamTracker.Services.SteamIds;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views;
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
        services.AddMatchmakerFeature(useFakes);
        services.AddPricePerHourFeature(useFakes);
        services.AddAchievementFeature(useFakes);
        services.AddSteamIdResolver(useFakes);
        services.AddFriendsRadarFeature(useFakes);
        services.AddComparisonFeature(useFakes);

        services.AddScreens();
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

    private static IServiceCollection AddMatchmakerFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            // FakeMatchmakerService додається разом з екраном (#88)
            return services;
        }

        // Потребує ISteamIdResolver — його реєструє #86
        services.AddSingleton<IMatchmakerService, MatchmakerService>();
        return services;
    }

    private static IServiceCollection AddPricePerHourFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            // FakePriceAnalyzerService додається разом з екраном (#85)
            return services;
        }

        // Store — інший хост і без ключа API, тому не в AddSteamInfrastructure
        services.AddHttpClient<IStoreClient, StoreClient>(http => new StoreClient(http))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton<IPriceAnalyzerService, PriceAnalyzerService>();
        return services;
    }

    private static IServiceCollection AddAchievementFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            services.AddSingleton<IAchievementService, FakeAchievementService>();
            return services;
        }

        services.AddSingleton<IAchievementService, AchievementService>();
        return services;
    }

    // Перевірка введених SteamID / посилань / ніків — спільна для Матчмейкера та інших екранів з вводом
    private static IServiceCollection AddSteamIdResolver(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            // Fake додається разом з екраном Матчмейкера (#88)
            return services;
        }

        services.AddSingleton<ISteamIdResolver, SteamIdResolver>();
        return services;
    }

    private static IServiceCollection AddFriendsRadarFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            services.AddSingleton<IFriendsRadarService, FakeFriendsRadarService>();
            return services;
        }

        services.AddSingleton<IFriendsRadarService, FriendsRadarService>();
        return services;
    }

    private static IServiceCollection AddComparisonFeature(this IServiceCollection services, bool useFakes)
    {
        if (useFakes)
        {
            // FakeComparisonService додається разом з екраном (#83)
            return services;
        }

        services.AddSingleton<IComparisonService, ComparisonService>();
        return services;
    }

    // ---------- екрани ----------

    private static IServiceCollection AddScreens(this IServiceCollection services)
    {
        services.AddSingleton<ViewFactory>();

        // Tag — значення Tag пункту меню в MainWindow.xaml. Новий екран — один рядок тут.
        // Друзі на Головній необов'язкові: у фейк-режимі IFriendsRadarService з'явиться лише з #75.
        // Після #75 можна повернути звичайний AddScreen<HomeView>.
        services.AddScreen("Home", sp => new HomeView(
            sp.GetRequiredService<ILibraryService>(),
            sp.GetRequiredService<IAuthService>(),
            sp.GetService<IFriendsRadarService>()));
        services.AddScreen<LibraryView>("Library");
        services.AddScreen<AchievementPlannerView>("AchievementPlanner");
        services.AddScreen<ComparisonView>("Comparison");
        services.AddScreen<FriendsRadarView>("FriendsRadar");
        services.AddScreen<MatchmakerView>("Matchmaker");
        services.AddScreen<PricePerHourView>("PricePerHour");
        return services;
    }

    // Transient: кожен перехід у меню створює свіжий екран зі своїм станом завантаження
    private static IServiceCollection AddScreen<TView>(this IServiceCollection services, string tag)
        where TView : class
    {
        services.AddTransient<TView>();
        services.AddSingleton(new ScreenRegistration(tag, typeof(TView)));
        return services;
    }

    // Варіант для екрана, якому потрібна необов'язкова залежність (GetService замість конструктора з DI)
    private static IServiceCollection AddScreen<TView>(
        this IServiceCollection services, string tag, Func<IServiceProvider, TView> factory)
        where TView : class
    {
        services.AddTransient(factory);
        services.AddSingleton(new ScreenRegistration(tag, typeof(TView)));
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
