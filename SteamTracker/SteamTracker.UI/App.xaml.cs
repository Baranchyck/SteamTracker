using System.Net.Http;
using System.Windows;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Integrations.SteamApi;
﻿using System.Windows;
using Microsoft.Extensions.Configuration;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Services.Configuration;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private HttpClient? _httpClient;

        public ISteamApiClient SteamApi { get; private set; } = null!;
        // TODO: перенести в DI-контейнер, коли він з'явиться
        internal SteamOptions SteamOptions { get; private set; } = null!;

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
            try
            {
                SteamOptions = SteamOptions.Load(BuildConfiguration());
            }
            catch (ConfigurationMissingException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "SteamTracker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
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