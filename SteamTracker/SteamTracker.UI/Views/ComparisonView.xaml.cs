using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views.Comparison;
using SteamTracker.UI.Views.FriendsRadar;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Екран «Час гри з друзями»: вибір гри з бібліотеки, рейтинг друзів за часом, хто без гри і в кого бібліотека прихована.
    /// </summary>
    public partial class ComparisonView : UserControl
    {
        private const double BarMaxWidth = 160;

        private readonly ILibraryService _library;
        private readonly IComparisonService _comparison;
        private CancellationTokenSource? _loadCts;

        // Ігри для випадного списку; завантажуються один раз, далі перемикається лише порівняння
        private IReadOnlyList<OwnedGame> _games = Array.Empty<OwnedGame>();

        // ItemsSource/SelectedItem виставляються з коду — це не вибір користувача
        private bool _settingGames;

        // XAML виставляє IsChecked ще в InitializeComponent, і обробники не повинні запускати завантаження раніше
        private readonly bool _isReady;

        public ComparisonView(ILibraryService library, IComparisonService comparison)
        {
            _library = library;
            _comparison = comparison;
            InitializeComponent();

#if DEBUG
            if (comparison is FakeComparisonService)
            {
                FakeModePanel.Visibility = Visibility.Visible;
            }
#endif

            _isReady = true;
            Loaded += OnLoaded;
            // Користувач пішов з екрана — запит більше не потрібен
            Unloaded += (_, _) => _loadCts?.Cancel();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadAsync(reloadGames: true);

        /// <param name="reloadGames">true — заново отримати бібліотеку для вибору гри, false — лише порівняння для вибраної гри.</param>
        private async Task LoadAsync(bool reloadGames)
        {
            // Попередній запит (інша гра) уже неактуальний
            _loadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _loadCts = cts;

            if (ContentPanel.Visibility == Visibility.Visible)
            {
                ContentPanel.Opacity = 0.5;
            }
            else
            {
                ShowState(StateKind.Loading, "Порівнюємо з друзями…",
                    "Уперше завантажуємо бібліотеки всіх друзів — це може зайняти кілька секунд. Бічне меню вже доступне.");
            }
            GameBox.IsEnabled = false;

            try
            {
                if (reloadGames || _games.Count == 0)
                {
                    var library = await _library.GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.PlaytimeDesc), cts.Token);
                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    switch (library.Status)
                    {
                        case DataStatus.Private:
                            ShowState(StateKind.Private, "Бібліотека прихована",
                                "Без списку ігор нема що порівнювати. Відкрий: Steam → Профіль → Налаштування приватності → Game details → Public.",
                                "Перевірити знову", ReloadAll);
                            return;
                        case DataStatus.Error:
                            ShowError(ReloadAll);
                            return;
                    }

                    SetGames(library.Value ?? Array.Empty<OwnedGame>());
                    if (_games.Count == 0)
                    {
                        ShowState(StateKind.Empty, "Ігор поки немає",
                            "Коли в бібліотеці з’являться ігри, тут можна буде порівняти свій час з друзями.",
                            "Оновити", ReloadAll);
                        return;
                    }
                }

                if (GameBox.SelectedItem is not OwnedGame game)
                {
                    return;
                }

                var result = await _comparison.GetComparisonAsync(game.AppId, cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                switch (result.Status)
                {
                    case DataStatus.Private:
                        ShowState(StateKind.Private, "Список друзів прихований",
                            "Steam не віддає список друзів. Відкрий: Steam → Профіль → Налаштування приватності → Список друзів → Відкрито.",
                            "Перевірити знову", ReloadComparison);
                        return;
                    case DataStatus.Error:
                        ShowError(ReloadComparison);
                        return;
                }

                var comparison = result.Value!;
                bool noFriends = comparison.Ranking.Count <= 1
                                 && comparison.FriendsWithoutGame.Count == 0
                                 && comparison.FriendsWithPrivateLibrary.Count == 0;
                if (noFriends)
                {
                    ShowState(StateKind.Empty, "Друзів поки немає",
                        "Додай друзів у Steam — і тут з’явиться, хто скільки грав у цю гру.",
                        "Оновити", ReloadComparison);
                    return;
                }

                Render(comparison);
                StatePanel.Hide();
                ContentPanel.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Скасовано: пішли з екрана або вже вибрано іншу гру
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowError(ReloadAll);
                }
            }
            finally
            {
                if (_loadCts == cts)
                {
                    _loadCts = null;
                    ContentPanel.Opacity = 1;
                    GameBox.IsEnabled = true;
                }
                cts.Dispose();
            }
        }

        private void SetGames(IReadOnlyList<OwnedGame> games)
        {
            // Після «Оновити» лишаємо вибрану гру, якщо вона ще є в бібліотеці
            int? selectedAppId = (GameBox.SelectedItem as OwnedGame)?.AppId;

            _settingGames = true;
            _games = games;
            GameBox.ItemsSource = games;
            GameBox.SelectedItem = games.FirstOrDefault(g => g.AppId == selectedAppId) ?? games.FirstOrDefault();
            GameBox.Visibility = games.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _settingGames = false;
        }

        private void Render(GameComparison comparison)
        {
            var me = comparison.Ranking.First(e => e.IsCurrentUser);
            var friends = comparison.Ranking.Where(e => !e.IsCurrentUser).ToList();

            PositionText.Text = $"#{comparison.UserRank}";
            PositionCaption.Text = $"з {ComparisonFormat.Players(comparison.Ranking.Count)}, у яких є ця гра";

            MyTimeText.Text = ComparisonFormat.Hours(me.PlaytimeForeverMinutes);
            var played = friends.Where(f => f.PlaytimeForeverMinutes > 0).ToList();
            MyTimeCaption.Text = comparison.PercentOfAverage is decimal percent && played.Count > 0
                ? $"{ComparisonFormat.Percent(percent)} від середнього серед друзів " +
                  $"({ComparisonFormat.Hours(played.Average(f => f.PlaytimeForeverMinutes))})"
                : "Ніхто з друзів ще не грав у цю гру";

            TwoWeeksText.Text = ComparisonFormat.Friends(friends.Count(f => f.Playtime2WeeksMinutes > 0));
            TwoWeeksCaption.Text = $"і ти: {ComparisonFormat.Hours(me.Playtime2WeeksMinutes)} за останні 14 днів";

            int maxMinutes = Math.Max(1, comparison.Ranking.Max(e => e.PlaytimeForeverMinutes));
            var primary = (Brush)FindResource("TextPrimary");
            var secondary = (Brush)FindResource("TextSecondary");
            var accent = (Brush)FindResource("Accent");
            var highlight = (Brush)FindResource("NavActive");

            RankingList.ItemsSource = comparison.Ranking
                .Select(e => new RankRow(
                    RankText: $"#{e.Rank}",
                    // Як у макеті: перші три місця й рядок користувача — яскравіші
                    RankBrush: e.Rank <= 3 || e.IsCurrentUser ? primary : secondary,
                    Initial: FriendStatus.Initial(e.PersonaName),
                    AvatarBrush: e.IsCurrentUser ? accent : FriendStatus.AvatarBrush(e.SteamId),
                    AvatarUrl: e.AvatarUrl,
                    Name: e.IsCurrentUser ? $"Ти ({e.PersonaName})" : e.PersonaName,
                    BadgeText: e.Playtime2WeeksMinutes > 0 ? $"+{ComparisonFormat.Hours(e.Playtime2WeeksMinutes)} за 2 тижні" : null,
                    BarWidth: BarMaxWidth * e.PlaytimeForeverMinutes / maxMinutes,
                    BarBrush: e.IsCurrentUser ? accent : secondary,
                    HoursText: ComparisonFormat.Hours(e.PlaytimeForeverMinutes),
                    RowBackground: e.IsCurrentUser ? highlight : Brushes.Transparent))
                .ToList();

            RenderFriends(WithoutGameCard, WithoutGameCount, WithoutGameList, comparison.FriendsWithoutGame);
            RenderFriends(PrivateCard, PrivateCount, PrivateList, comparison.FriendsWithPrivateLibrary);
        }

        private static void RenderFriends(Border card, TextBlock count, ItemsControl list, IReadOnlyList<SteamProfile> friends)
        {
            card.Visibility = friends.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            count.Text = friends.Count.ToString();
            list.ItemsSource = friends
                .Select(f => new FriendItem(FriendStatus.Initial(f.PersonaName), FriendStatus.AvatarBrush(f.SteamId), f.AvatarUrl, f.PersonaName))
                .ToList();
        }

        private void ShowError(Action retry) =>
            ShowState(StateKind.Error, "Не вдалося отримати дані",
                "Немає інтернету або Steam не відповідає. Застосунок працює далі.",
                "Повторити", retry);

        private void ShowState(StateKind kind, string title, string message, string? actionText = null, Action? action = null)
        {
            ContentPanel.Visibility = Visibility.Collapsed;
            StatePanel.Show(kind, title, message, actionText, action);
        }

        private async void ReloadAll() => await LoadAsync(reloadGames: true);

        private async void ReloadComparison() => await LoadAsync(reloadGames: false);

        private async void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady && !_settingGames && GameBox.SelectedItem is OwnedGame)
            {
                await LoadAsync(reloadGames: false);
            }
        }

        private async void FakeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isReady || _comparison is not FakeComparisonService fake || e.OriginalSource is not RadioButton { Tag: string mode })
            {
                return;
            }

            fake.ReturnEmpty = mode == "Empty";
            fake.Status = mode switch
            {
                "Error" => DataStatus.Error,
                "Private" => DataStatus.Private,
                _ => DataStatus.Available
            };

            await LoadAsync(reloadGames: false);
        }
    }
}
