using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamTracker.DataAccess;
using SteamTracker.Domain.Exceptions;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
        private ServiceProvider? _provider;

        public IServiceProvider Services => _provider
            ?? throw new InvalidOperationException("Контейнер ще не ініціалізовано.");

        protected override void OnStartup(StartupEventArgs e)
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

            _provider.GetRequiredService<MainWindow>().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _provider?.Dispose(); // закриває і singleton-и, і HttpClient
            base.OnExit(e);
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
