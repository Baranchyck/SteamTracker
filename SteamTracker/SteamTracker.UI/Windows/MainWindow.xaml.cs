using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Views;

namespace SteamTracker.UI.Windows
{
    /// <summary>
    /// Головне вікно: бічне меню навігації, картка поточного користувача та область для екранів модулів.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IAuthService _auth;

        public MainWindow(IAuthService auth)
        {
            InitializeComponent();
            _auth = auth;

            ShowUser(_auth.CurrentUser);
            _auth.CurrentUserChanged += Auth_CurrentUserChanged;

            HomeNavItem.IsChecked = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _auth.CurrentUserChanged -= Auth_CurrentUserChanged;
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

            (VisibilityText.Text, VisibilityText.Foreground) = user.Visibility switch
            {
                ProfileVisibility.Public => ("Публічний профіль", (Brush)FindResource("Success")),
                ProfileVisibility.Limited => ("Обмежений профіль", (Brush)FindResource("TextSecondary")),
                _ => ("Приватний профіль", (Brush)FindResource("Danger"))
            };
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

        // Спільний обробник для всіх пунктів меню: Tag пункту визначає, який екран показати
        private void NavItem_Checked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is RadioButton { Tag: string screen })
            {
                MainContent.Content = CreateView(screen);
            }
        }

        private static UserControl CreateView(string screen) => screen switch
        {
            "Home" => new HomeView(),
            "AchievementPlanner" => new AchievementPlannerView(),
            "Comparison" => new ComparisonView(),
            "FriendsRadar" => new FriendsRadarView(),
            "Matchmaker" => new MatchmakerView(),
            "PricePerHour" => new PricePerHourView(),
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, "Unknown screen")
        };
    }
}
