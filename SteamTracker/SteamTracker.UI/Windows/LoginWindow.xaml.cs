using System.Windows;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;

namespace SteamTracker.UI.Windows
{
    /// <summary>
    /// Вікно входу через Steam: кнопка входу, стан очікування зі скасуванням, повідомлення про помилку.
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly IAuthService _auth;
        private CancellationTokenSource? _loginCts;

        /// <summary>Вхід успішний: App відкриває головне вікно.</summary>
        public event EventHandler? LoggedIn;

        public LoginWindow(IAuthService auth)
        {
            InitializeComponent();
            _auth = auth;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            _loginCts = new CancellationTokenSource();
            SetWaiting(true);

            try
            {
                await _auth.LoginAsync(_loginCts.Token);
                LoggedIn?.Invoke(this, EventArgs.Empty);
            }
            catch (AuthCancelledException)
            {
                // Користувач сам скасував вхід: просто повертаємось до звичайного стану
            }
            catch (AuthFailedException)
            {
                ShowError("Не вдалося увійти через Steam. Спробуй ще раз.");
            }
            finally
            {
                _loginCts.Dispose();
                _loginCts = null;
                SetWaiting(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _loginCts?.Cancel();
        }

        // Закриття вікна під час очікування теж скасовує вхід
        protected override void OnClosed(EventArgs e)
        {
            _loginCts?.Cancel();
            base.OnClosed(e);
        }

        private void SetWaiting(bool isWaiting)
        {
            LoginButton.IsEnabled = !isWaiting;
            WaitingPanel.Visibility = isWaiting ? Visibility.Visible : Visibility.Collapsed;

            if (isWaiting)
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
