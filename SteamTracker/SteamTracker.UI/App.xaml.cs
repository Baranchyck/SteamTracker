using System.Net.Http;
using System.Windows;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Integrations.SteamApi;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private HttpClient? _httpClient;

        public ISteamApiClient SteamApi { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var factory = new SqliteConnectionFactory(SqliteConnectionFactory.GetDefaultPath());
                new DatabaseInitializer(factory).Initialize();
            }
            catch (Exception ex)
            {
                // TODO: залогувати ex (коли з'явиться логер)
                MessageBox.Show(
                    "Не вдалося ініціалізувати базу даних.\n" + ex.Message,
                    "SteamTracker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            _httpClient = new HttpClient();
            SteamApi = new SteamApiClient(_httpClient);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _httpClient?.Dispose();
            base.OnExit(e);
        }
    }
}