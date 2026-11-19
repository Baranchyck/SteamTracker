using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Views;
using SteamTracker.UI.Views.Profile;

namespace SteamTracker.UI.Windows
{
    /// <summary>
    /// Головне вікно: бічне меню навігації, картка поточного користувача та область для екранів модулів.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Статус у Steam міняється щохвилини, тож картку оновлюємо періодично, а не лише при вході
        private static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromMinutes(1);

        private readonly IAuthService _auth;
        private readonly IProfileStatusService _status;
        private readonly ViewFactory _views;
        private readonly DispatcherTimer _statusTimer;
        private CancellationTokenSource? _statusCts;

        public MainWindow(IAuthService auth, IProfileStatusService status, ViewFactory views)
        {
            InitializeComponent();
            _auth = auth;
            _status = status;
            _views = views;

            _statusTimer = new DispatcherTimer { Interval = StatusRefreshInterval };
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            ShowUser(_auth.CurrentUser);
            _auth.CurrentUserChanged += Auth_CurrentUserChanged;

            // Екрани просять перейти на інший екран через Navigation.Request (напр. картка друзів на Головній)
            MainContent.AddHandler(Navigation.RequestedEvent, new NavigationRequestedEventHandler(Navigation_Requested));

            HomeNavItem.IsChecked = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _auth.CurrentUserChanged -= Auth_CurrentUserChanged;
            _statusTimer.Stop();
            _statusCts?.Cancel();
            base.OnClosed(e);
        }

        // Подія може прийти не з UI-потоку, тому оновлення картки передаємо в UI-потік через Dispatcher
        private void Auth_CurrentUserChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() => ShowUser(_auth.CurrentUser));
        }

        private void ShowUser(SteamProfile? user)
        {
            // null — користувач вийшов: App вже повертає на екран входу й закриває це вікно
            if (user is null)
            {
                return;
            }

            UserNameText.Text = user.PersonaName;
            AvatarInitial.Text = string.IsNullOrEmpty(user.PersonaName) ? "?" : user.PersonaName[..1].ToUpperInvariant();
            ShowAvatar(user.AvatarUrl);

            // Поки статус вантажиться, рядок порожній, щоб не блимав «Публічний профіль» → «Онлайн»
            StatusText.Text = string.Empty;
            StatusText.ToolTip = null;
            StatusDot.Visibility = Visibility.Collapsed;
            _ = RefreshStatusAsync();
        }

        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            await RefreshStatusAsync();
        }

        // Викликається з UI-потоку (ShowUser, таймер), тож після await ми знову в UI-потоці
        private async Task RefreshStatusAsync()
        {
            var user = _auth.CurrentUser;
            if (user is null)
            {
                return;
            }

            // Новий запит скасовує попередній, щоб повільна стара відповідь не перезаписала свіжу
            _statusCts?.Cancel();
            var cts = new CancellationTokenSource();
            _statusCts = cts;

            PlayerPresence? presence;
            try
            {
                presence = await _status.GetCurrentStatusAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Поки чекали, міг стартувати новіший запит або змінитися користувач
            if (cts != _statusCts || _auth.CurrentUser?.SteamId != user.SteamId)
            {
                return;
            }

            var (text, brushKey) = ProfileStatusFormat.Of(presence, user.Visibility);
            var brush = (Brush)FindResource(brushKey);
            StatusText.Text = text;
            StatusText.ToolTip = text;
            StatusText.Foreground = brush;
            StatusDot.Fill = brush;
            StatusDot.Visibility = Visibility.Visible;
        }

        // Якщо URL порожній або картинка не завантажилась, лишається коло з першою літерою ніка
        private void ShowAvatar(string avatarUrl)
        {
            AvatarImage.Visibility = Visibility.Collapsed;

            if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri))
            {
                return;
            }

            var bitmap = new BitmapImage(uri);
            bitmap.DownloadFailed += (_, _) => AvatarImage.Visibility = Visibility.Collapsed;
            bitmap.DecodeFailed += (_, _) => AvatarImage.Visibility = Visibility.Collapsed;

            AvatarImage.Fill = new ImageBrush(bitmap);
            AvatarImage.Visibility = Visibility.Visible;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutButton.IsEnabled = false;
            // Після виходу App отримає CurrentUserChanged і поверне на екран входу
            await _auth.LogoutAsync();
        }

        // Відмічаємо пункт меню з потрібним Tag — далі все робить NavItem_Checked, і меню показує правильний пункт
        private void Navigation_Requested(object sender, NavigationRequestedEventArgs e)
        {
            var item = NavPanel.Children.OfType<RadioButton>().FirstOrDefault(r => Equals(r.Tag, e.Screen));
            if (item is not null)
            {
                item.IsChecked = true;
                e.Handled = true;
            }
        }

        // Спільний обробник для всіх пунктів меню: Tag пункту визначає, який екран показати
        private void NavItem_Checked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is RadioButton { Tag: string screen })
            {
                MainContent.Content = _views.Create(screen);
            }
        }
    }
}
