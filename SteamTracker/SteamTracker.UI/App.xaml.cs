using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.UI.Logging;
using SteamTracker.UI.Windows;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private ServiceProvider? _provider;
        private ILoggerFactory? _loggerFactory;
        private ILogger<App>? _logger;

        public IServiceProvider Services => _provider
            ?? throw new InvalidOperationException("Контейнер ще не ініціалізовано.");

        private IAuthService Auth => Services.GetRequiredService<IAuthService>();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = BuildConfiguration();
            _loggerFactory = LoggingSetup.CreateLoggerFactory(configuration);
            var logger = _logger = _loggerFactory.CreateLogger<App>();
            logger.LogInformation("SteamTracker запущено (fakes: {UseFakes})",
                configuration.GetValue<bool>(ServiceCollectionExtensions.UseFakesKey));

            try
            {
                _provider = new ServiceCollection()
                    .AddAppLogging(_loggerFactory)
                    .AddSteamTracker(configuration)
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        // Невирішена залежність падає тут, а не при першому кліку в UI.
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });
            }
            catch (ConfigurationMissingException ex)
            {
                // Очікуваний збій налаштування (немає ключа), а не баг — стек не потрібен
                logger.LogWarning("Конфігурація неповна: {Message}", ex.Message);
                Fail(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не вдалося зібрати контейнер залежностей");
                Fail("Не вдалося зібрати контейнер залежностей.\n" + ex.Message);
                return;
            }

            try
            {
                _provider.GetRequiredService<DatabaseInitializer>().Initialize();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не вдалося ініціалізувати базу даних");
                Fail("Не вдалося ініціалізувати базу даних.\n" + ex.Message);
                return;
            }

            Auth.CurrentUserChanged += Auth_CurrentUserChanged;

            // Стартовий флоу: збережена сесія -> головне вікно, інакше -> вхід
            var user = await Auth.TryRestoreSessionAsync();
            if (user is null)
            {
                ShowLoginWindow();
            }
            else
            {
                ShowMainWindow();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _logger?.LogInformation("SteamTracker завершено (код {ExitCode})", e.ApplicationExitCode);
            _provider?.Dispose(); // закриває і singleton-и, і HttpClient
            _loggerFactory?.Dispose(); // останнім: під час Dispose сервіси ще можуть писати в лог
            base.OnExit(e);
        }

        private void ShowLoginWindow()
        {
            var login = Services.GetRequiredService<LoginWindow>();
            login.LoggedIn += (_, _) =>
            {
                // Спершу відкриваємо головне вікно, потім закриваємо вікно входу,
                // інакше застосунок завершиться, бо закрилося останнє вікно
                ShowMainWindow();
                login.Close();
            };
            login.Show();
        }

        private void ShowMainWindow()
        {
            MainWindow = Services.GetRequiredService<SteamTracker.UI.Windows.MainWindow>();
            MainWindow.Show();
        }

        // Подія може прийти не з UI-потоку, тому роботу з вікнами передаємо в UI-потік через Dispatcher
        private void Auth_CurrentUserChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Користувач вийшов, поки відкрите головне вікно: повертаємось на екран входу
                if (Auth.CurrentUser is null && MainWindow is SteamTracker.UI.Windows.MainWindow main)
                {
                    ShowLoginWindow();
                    main.Close();
                }
            });
        }

        private void Fail(string message)
        {
            MessageBox.Show(message, "SteamTracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }

        // user-secrets додаються останніми, тож на машині розробника перекривають appsettings.json.
        // У зібраному .exe user-secrets немає, і ключ береться з appsettings.json поруч із ним.
        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<App>(optional: true)
                .Build();
    }
}