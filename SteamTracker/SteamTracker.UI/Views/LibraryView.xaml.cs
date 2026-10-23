using System.Windows;
using System.Windows.Controls;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views.Library;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Екран «Бібліотека»: статистика, усі ігри (сортування + фільтр) і ігри, у які майже не грав.
    /// </summary>
    public partial class LibraryView : UserControl
    {
        private const int BarelyPlayedMinutes = 60;
        private const double BarMaxWidth = 110;

        private readonly ILibraryService _library;
        private CancellationTokenSource? _loadCts;

        // XAML виставляє початкові значення (IsSelected, IsChecked) ще в InitializeComponent,
        // і їхні обробники не повинні запускати завантаження до того, як екран готовий
        private readonly bool _isReady;

        public LibraryView(ILibraryService library)
        {
            _library = library;
            InitializeComponent();

#if DEBUG
            if (library is FakeLibraryService)
            {
                FakeModePanel.Visibility = Visibility.Visible;
            }
#endif

            _isReady = true;
            Loaded += OnLoaded;
            // Користувач пішов з екрана — запит більше не потрібен
            Unloaded += (_, _) => _loadCts?.Cancel();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadAsync(includeOverview: true);

        private async void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady)
            {
                await LoadAsync(includeOverview: false);
            }
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_isReady)
            {
                await LoadAsync(includeOverview: false);
            }
        }

        /// <param name="includeOverview">
        /// true — перезавантажити весь екран (картки, «Майже не грав»), false — лише список «Усі ігри».
        /// </param>
        private async Task LoadAsync(bool includeOverview)
        {
            // Попередній запит (інше сортування/фільтр) уже неактуальний
            _loadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _loadCts = cts;

            if (includeOverview)
            {
                ShowState(StateKind.Loading, "Завантажуємо бібліотеку…",
                    "Список ігор з’явиться за кілька секунд. Бічне меню вже доступне.");
            }
            else
            {
                GamesList.Opacity = 0.5;
            }

            try
            {
                // Статистика й «Майже не грав» рахуються по всій бібліотеці, без фільтра користувача.
                // Запити по черзі, а не паралельно: перший заповнює кеш LibraryService, другий бере з нього,
                // тож у Steam іде один запит
                ServiceResult<IReadOnlyList<OwnedGame>>? overview = null;
                if (includeOverview)
                {
                    overview = await _library.GetMyLibraryAsync(new LibraryFilter(Sort: LibrarySort.PlaytimeDesc), cts.Token);
                    if (cts.IsCancellationRequested || ShowFailureIfAny(overview))
                    {
                        return;
                    }
                }

                var filter = new LibraryFilter(NeverPlayedCheck.IsChecked == true, SelectedSort());
                var list = await _library.GetMyLibraryAsync(filter, cts.Token);

                if (cts.IsCancellationRequested || ShowFailureIfAny(list))
                {
                    return;
                }

                if (overview is not null)
                {
                    var allGames = overview.Value ?? Array.Empty<OwnedGame>();
                    if (allGames.Count == 0)
                    {
                        ShowState(StateKind.Empty, "Бібліотека порожня",
                            "На акаунті ще немає ігор. Статистика з’явиться після першої гри.",
                            "Оновити", Reload);
                        return;
                    }

                    RenderOverview(allGames);
                }

                RenderGames(list.Value ?? Array.Empty<OwnedGame>());
                StatePanel.Hide();
                ContentPanel.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Скасовано: пішли з екрана або вже запущено новий запит
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowError();
                }
            }
            finally
            {
                if (_loadCts == cts)
                {
                    _loadCts = null;
                    GamesList.Opacity = 1;
                }
                cts.Dispose();
            }
        }

        private void RenderOverview(IReadOnlyList<OwnedGame> allGames)
        {
            var barelyPlayed = allGames
                .Where(g => g.PlaytimeForeverMinutes < BarelyPlayedMinutes)
                .OrderBy(g => g.PlaytimeForeverMinutes)
                .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            GamesCountText.Text = LibraryFormat.Count(allGames.Count);
            TotalHoursText.Text = LibraryFormat.TotalHours(allGames.Sum(g => (long)g.PlaytimeForeverMinutes));
            BarelyCountText.Text = LibraryFormat.Count(barelyPlayed.Count);
            BarelyBadgeText.Text = LibraryFormat.Count(barelyPlayed.Count);

            BarelyList.ItemsSource = barelyPlayed
                .Select(g => new BarelyPlayedRow(g.Name, g.IconUrl, g.PlaytimeForeverMinutes == 0,
                    LibraryFormat.Minutes(g.PlaytimeForeverMinutes)))
                .ToList();
            NoBarelyText.Visibility = barelyPlayed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RenderGames(IReadOnlyList<OwnedGame> games)
        {
            int maxMinutes = games.Count == 0 ? 0 : games.Max(g => g.PlaytimeForeverMinutes);

            var rows = games
                .Select(g => new GameRow(g.Name, g.IconUrl, LibraryFormat.LastPlayed(g.LastPlayed),
                    LibraryFormat.Hours(g.PlaytimeForeverMinutes), BarWidth(g.PlaytimeForeverMinutes, maxMinutes)))
                .ToList();

            GamesList.ItemsSource = rows;
            if (rows.Count > 0)
            {
                GamesList.ScrollIntoView(rows[0]);
            }

            CounterText.Text = $"{LibraryFormat.Games(games.Count)} · " +
                               LibraryFormat.TotalHours(games.Sum(g => (long)g.PlaytimeForeverMinutes));
            NoMatchesText.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Ненульовий час — щонайменше крапка (6 px), як у макеті для ігор з малим часом
        private static double BarWidth(int minutes, int maxMinutes) =>
            minutes == 0 || maxMinutes == 0 ? 0 : Math.Max(6, BarMaxWidth * minutes / maxMinutes);

        private bool ShowFailureIfAny(ServiceResult<IReadOnlyList<OwnedGame>> result)
        {
            switch (result.Status)
            {
                case DataStatus.Private:
                    ShowState(StateKind.Private, "Профіль приватний",
                        "Steam не віддає список ігор. Відкрий: Steam → Профіль → Налаштування приватності → Game details → Public.",
                        "Перевірити знову", Reload);
                    return true;
                case DataStatus.Error:
                    ShowError();
                    return true;
                default:
                    return false;
            }
        }

        private void ShowError() =>
            ShowState(StateKind.Error, "Не вдалося отримати дані",
                "Немає інтернету або Steam не відповідає. Застосунок працює далі.",
                "Повторити", Reload);

        private void ShowState(StateKind kind, string title, string message, string? actionText = null, Action? action = null)
        {
            ContentPanel.Visibility = Visibility.Collapsed;
            StatePanel.Show(kind, title, message, actionText, action);
        }

        private async void Reload() => await LoadAsync(includeOverview: true);

        private LibrarySort SelectedSort() =>
            SortBox.SelectedItem is ComboBoxItem { Tag: LibrarySort sort } ? sort : LibrarySort.PlaytimeDesc;

        private async void FakeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isReady || _library is not FakeLibraryService fake || e.OriginalSource is not RadioButton { Tag: string mode })
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

            await LoadAsync(includeOverview: true);
        }
    }
}
