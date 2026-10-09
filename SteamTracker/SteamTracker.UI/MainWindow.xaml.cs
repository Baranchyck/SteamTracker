using System.Windows;
using System.Windows.Controls;
using SteamTracker.UI.Views;

namespace SteamTracker.UI
{
    /// <summary>
    /// Головне вікно: бічне меню навігації та область для екранів модулів.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            HomeNavItem.IsChecked = true;
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
