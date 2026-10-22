using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.UI.Windows;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private ServiceProvider? _provider;

        public IServiceProvider Services => _provider
            ?? throw new InvalidOperationException("Контейнер ще не ініціалізовано.");

        private IAuthService Auth => Services.GetRequiredService<IAuthService>();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _provider = new ServiceCollection()
                    .AddSteamTracker(BuildConfiguration())
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        // Невирішена залежність падає тут, а не при першому кліку в UI.
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });
            }
            catch (ConfigurationMissingException ex)
            {
                Fail(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                // TODO: залогувати ex (коли з'явиться логер)
                Fail("Не вдалося зібрати контейнер залежностей.\n" + ex.Message);
                return;
            }

            try
            {
                _provider.GetRequiredService<DatabaseInitializer>().Initialize();
            }
            catch (Exception ex)
            {
                // TODO: залогувати ex
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
            _provider?.Dispose(); // закриває і singleton-и, і HttpClient
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
