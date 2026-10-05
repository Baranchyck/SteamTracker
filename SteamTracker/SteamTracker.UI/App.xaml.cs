using System.Windows;
using SteamTracker.DataAccess;

namespace SteamTracker.UI
{
    public partial class App : Application
    {
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
        }
    }
}