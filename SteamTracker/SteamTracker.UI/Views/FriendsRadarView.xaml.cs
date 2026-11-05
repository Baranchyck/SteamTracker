using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views.FriendsRadar;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Екран «Друзі-радар»: хто з друзів онлайн і в що грає, фільтр «Вільні зараз», «Оновити».
    /// </summary>
    public partial class FriendsRadarView : UserControl
    {
        private readonly IFriendsRadarService _radar;
        private CancellationTokenSource? _loadCts;

        // Повний список із останнього оновлення: з нього рахуються легенда й лічильники фільтра
        private IReadOnlyList<PlayerPresence> _all = Array.Empty<PlayerPresence>();

        // XAML виставляє IsChecked ще в InitializeComponent, і обробник фільтра не повинен запускати завантаження раніше
        private readonly bool _isReady;

        public FriendsRadarView(IFriendsRadarService radar)
        {
            _radar = radar;
            InitializeComponent();

#if DEBUG
            if (radar is FakeFriendsRadarService)
            {
                FakeModePanel.Visibility = Visibility.Visible;
            }
#endif

            _isReady = true;
            Loaded += OnLoaded;
            // Користувач пішов з екрана — запит більше не потрібен
            Unloaded += (_, _) => _loadCts?.Cancel();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadAsync(includeAll: true);

        /// <param name="includeAll">
        /// true — заново отримати всіх друзів (легенда, лічильники), false — лише перемкнути фільтр.
        /// </param>
        private async Task LoadAsync(bool includeAll)
        {
            // Попередній запит (інший фільтр чи старе оновлення) уже неактуальний
            _loadCts?.Cancel();
            var cts = new CancellationTokenSource();
            _loadCts = cts;

            if (ContentPanel.Visibility == Visibility.Visible)
            {
                FriendsList.Opacity = 0.5;
            }
            else
            {
                ShowState(StateKind.Loading, "Шукаємо друзів…", "Статуси з’являться за кілька секунд. Бічне меню вже доступне.");
            }
            RefreshButton.IsEnabled = false;

            try
            {
                if (includeAll)
                {
                    var all = await _radar.GetFriendsAsync(new RadarFilter(), cts.Token);
                    if (cts.IsCancellationRequested || ShowFailureIfAny(all.Status))
                    {
                        return;
                    }

                    _all = all.Value ?? Array.Empty<PlayerPresence>();
                    if (_all.Count == 0)
                    {
                        ShowState(StateKind.Empty, "Друзів поки немає",
                            "Додай друзів у Steam — тут з’явиться, хто з них онлайн і в що грає.",
                            "Оновити", Reload);
                        return;
                    }

                    RenderCounters();
                }

                // Фільтр «Вільні зараз» застосовує сервіс
                var shown = _all;
                if (AvailableFilter.IsChecked == true)
                {
                    var available = await _radar.GetFriendsAsync(new RadarFilter(OnlyAvailable: true), cts.Token);
                    if (cts.IsCancellationRequested || ShowFailureIfAny(available.Status))
                    {
                        return;
                    }

                    shown = available.Value ?? Array.Empty<PlayerPresence>();
                }

                RenderFriends(shown);
                StatePanel.Hide();
                ContentPanel.Visibility = Visibility.Visible;
                ToolsPanel.Visibility = Visibility.Visible;
                LegendPanel.Visibility = Visibility.Visible;
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
                    FriendsList.Opacity = 1;
                    RefreshButton.IsEnabled = true;
                }
                cts.Dispose();
            }
        }

        private void RenderCounters()
        {
            var groups = _all.GroupBy(FriendStatus.GroupOf).ToDictionary(g => g.Key, g => g.Count());
            int Count(FriendGroup group) => groups.TryGetValue(group, out int count) ? count : 0;

            InGameCountText.Text = $"У грі · {Count(FriendGroup.InGame)}";
            OnlineCountText.Text = $"Онлайн · {Count(FriendGroup.Online)}";
            AwayCountText.Text = $"Відійшли · {Count(FriendGroup.Away)}";
            OfflineCountText.Text = $"Офлайн · {Count(FriendGroup.Offline)}";

            AllFilterText.Text = $"Усі · {_all.Count}";
            AvailableFilterText.Text = $"Вільні зараз · {_all.Count(FriendStatus.IsAvailable)}";
        }

        private void RenderFriends(IReadOnlyList<PlayerPresence> friends)
        {
            FriendsList.ItemsSource = friends
                .Select(f =>
                {
                    var group = FriendStatus.GroupOf(f);
                    return new FriendRow(
                        f.PersonaName,
                        FriendStatus.Initial(f.PersonaName),
                        f.AvatarUrl,
                        FriendStatus.AvatarBrush(f.SteamId),
                        FriendStatus.Text(f),
                        (Brush)FindResource(FriendStatus.BrushKey(group)),
                        group == FriendGroup.InGame ? f.CurrentGameName : null,
                        // Офлайн-друзі приглушені, як у макеті
                        group == FriendGroup.Offline ? 0.55 : 1);
                })
                .ToList();

            NoAvailableText.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool ShowFailureIfAny(DataStatus status)
        {
            switch (status)
            {
                case DataStatus.Private:
                    ShowState(StateKind.Private, "Список друзів прихований",
                        "Steam не віддає список друзів. Відкрий: Steam → Профіль → Налаштування приватності → Список друзів → Відкрито.",
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
            ToolsPanel.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Collapsed;
            StatePanel.Show(kind, title, message, actionText, action);
        }

        private async void Reload() => await LoadAsync(includeAll: true);

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync(includeAll: true);

        private async void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (_isReady && ContentPanel.Visibility == Visibility.Visible)
            {
                await LoadAsync(includeAll: false);
            }
        }

        private async void FakeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isReady || _radar is not FakeFriendsRadarService fake || e.OriginalSource is not RadioButton { Tag: string mode })
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

            await LoadAsync(includeAll: true);
        }
    }
}
