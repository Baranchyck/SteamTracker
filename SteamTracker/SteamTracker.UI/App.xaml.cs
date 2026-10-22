using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Integrations.Auth;
using SteamTracker.Integrations.SteamApi;
using SteamTracker.Services.Auth;
using SteamTracker.Services.Configuration;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Windows;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private HttpClient? _httpClient;

        // TODO: перенести в DI-контейнер (#49)
        public ISteamApiClient SteamApi { get; private set; } = null!;
        public IAuthService Auth { get; private set; } = null!;
        internal SteamOptions SteamOptions { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = BuildConfiguration();
            // UseFakes=true — фейкові сервіси без Steam і без мережі (див. Fakes/)
            var useFakes = configuration.GetValue<bool>("UseFakes");

            IDbConnectionFactory factory;
            try
            {
                factory = new SqliteConnectionFactory(SqliteConnectionFactory.GetDefaultPath());
                new DatabaseInitializer(factory).Initialize();
            }
            catch (Exception ex)
            {
                // TODO: залогувати ex (коли з'явиться логер)
                ShowStartupError("Не вдалося ініціалізувати базу даних.\n" + ex.Message);
                return;
            }

            _httpClient = new HttpClient();
            SteamApi = new SteamApiClient(_httpClient);

            if (useFakes)
            {
                Auth = new FakeAuthService
                {
                    HasSavedSession = configuration.GetValue<bool>("Fakes:HasSavedSession"),
                    SimulateFailure = configuration.GetValue<bool>("Fakes:SimulateFailure")
                };
            }
            else
            {
                try
                {
                    SteamOptions = SteamOptions.Load(configuration);
                }
                catch (ConfigurationMissingException ex)
                {
                    ShowStartupError(ex.Message);
                    return;
                }

                Auth = new AuthService(
                    new SteamOpenIdAuthenticator(_httpClient),
                    SteamApi,
                    new SqliteUserRepository(factory),
                    new SqliteSettingsRepository(factory));
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
            _httpClient?.Dispose();
            base.OnExit(e);
        }

        private void ShowLoginWindow()
        {
            var login = new LoginWindow(Auth);
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
            MainWindow = new MainWindow();
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

        private void ShowStartupError(string message)
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
