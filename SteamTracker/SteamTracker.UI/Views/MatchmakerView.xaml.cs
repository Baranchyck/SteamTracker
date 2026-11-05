using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views.FriendsRadar;
using SteamTracker.UI.Views.Library;
using SteamTracker.UI.Views.Matchmaker;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Екран «Co-op Matchmaker»: вибір учасників (друзі зі списку або гравець за SteamID / посиланням / ніком)
    /// і список ігор, які є в усіх, від найпопулярнішої в компанії.
    /// </summary>
    public partial class MatchmakerView : UserControl
    {
        // Сам користувач завжди в складі, тож вибрати можна на одного менше за максимум
        private const int MaxFriends = IMatchmakerService.MaxParticipants - 1;

        private readonly IMatchmakerService _matchmaker;
        private readonly IFriendsRadarService _radar;
        private readonly IAuthService _auth;
        private readonly ObservableCollection<ParticipantRow> _rows = new();

        private CancellationTokenSource? _friendsCts;
        private CancellationTokenSource? _searchCts;

        // Після першого пошуку перемикач фейкового режиму одразу перезапускає його
        private bool _hasSearched;

        // XAML виставляє IsChecked ще в InitializeComponent, і обробник перемикача не повинен нічого запускати раніше
        private readonly bool _isReady;

        public MatchmakerView(IMatchmakerService matchmaker, IFriendsRadarService radar, IAuthService auth)
        {
            _matchmaker = matchmaker;
            _radar = radar;
            _auth = auth;
            InitializeComponent();

            ParticipantsList.ItemsSource = _rows;

#if DEBUG
            if (matchmaker is FakeMatchmakerService)
            {
                FakeModePanel.Visibility = Visibility.Visible;
            }
#endif

            _isReady = true;
            UpdateSelectionState();
            Loaded += OnLoaded;
            // Користувач пішов з екрана — запити більше не потрібні
            Unloaded += (_, _) =>
            {
                _friendsCts?.Cancel();
                _searchCts?.Cancel();
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadFriendsAsync();

        // ---------- склад ----------

        private async Task LoadFriendsAsync()
        {
            _friendsCts?.Cancel();
            var cts = new CancellationTokenSource();
            _friendsCts = cts;

            if (_rows.Count == 0 && _auth.CurrentUser is { } user)
            {
                // Сам користувач: завжди в складі, зняти галочку не можна
                AddRow(new ParticipantRow($"Ти ({user.PersonaName})", user.SteamId, user.AvatarUrl,
                    FriendStatus.AvatarBrush(user.SteamId), FriendStatus.Initial(user.PersonaName), true, user.SteamId));
            }

            try
            {
                var friends = await _radar.GetFriendsAsync(new RadarFilter(), cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (friends.Status != DataStatus.Available)
                {
                    ShowFriendsHint(friends.Status == DataStatus.Private
                        ? "Список друзів прихований у Steam. Додай гравців за SteamID нижче."
                        : "Не вдалося завантажити друзів. Додай гравців за SteamID нижче.");
                    return;
                }

                foreach (var friend in friends.Value ?? Array.Empty<PlayerPresence>())
                {
                    AddRow(new ParticipantRow(friend.PersonaName, friend.SteamId, friend.AvatarUrl,
                        FriendStatus.AvatarBrush(friend.SteamId), FriendStatus.Initial(friend.PersonaName), false, friend.SteamId));
                }

                if (friends.Value is not { Count: > 0 })
                {
                    ShowFriendsHint("У тебе поки немає друзів у Steam. Додай гравців за SteamID нижче.");
                }

                UpdateSelectionState();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Скасовано: пішли з екрана
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowFriendsHint("Не вдалося завантажити друзів. Додай гравців за SteamID нижче.");
                }
            }
            finally
            {
                if (_friendsCts == cts)
                {
                    _friendsCts = null;
                }
                cts.Dispose();
            }
        }

        private void ShowFriendsHint(string text)
        {
            FriendsHintText.Text = text;
            FriendsHintText.Visibility = Visibility.Visible;
        }

        private void AddRow(ParticipantRow row)
        {
            row.PropertyChanged += Row_PropertyChanged;
            _rows.Add(row);
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ParticipantRow.IsSelected))
            {
                return;
            }

            // «Бібліотека прихована» стосується лише останнього пошуку: зняли галочку — значок зникає
            if (sender is ParticipantRow { IsSelected: false, LibraryHidden: true } row)
            {
                row.LibraryHidden = false;
            }

            UpdateSelectionState();
        }

        private int SelectedFriends => _rows.Count(r => !r.IsCurrentUser && r.IsSelected);

        /// <summary>Блокує вільні галочки після ліміту й оновлює кнопку пошуку («· 4 учасники»).</summary>
        private void UpdateSelectionState()
        {
            int selected = SelectedFriends;
            foreach (var row in _rows.Where(r => !r.IsCurrentUser))
            {
                row.CanToggle = row.IsSelected || selected < MaxFriends;
            }

            FindButton.Content = MatchmakerFormat.FindButton(selected + 1);
            // Потрібен хоча б один інший учасник; під час пошуку кнопка недоступна
            FindButton.IsEnabled = selected >= 1 && _searchCts is null;
        }

        // ---------- додати гравця вручну ----------

        private void Add_Click(object sender, RoutedEventArgs e) => AddManual();

        private void AddBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddManual();
                e.Handled = true;
            }
        }

        private void AddBox_TextChanged(object sender, TextChangedEventArgs e) =>
            AddMessageText.Visibility = Visibility.Collapsed;

        private void AddManual()
        {
            string text = AddBox.Text.Trim();
            if (text.Length == 0)
            {
                return;
            }

            // Той самий гравець, що вже в списку (за SteamID чи ніком), просто вибираємо, а не дублюємо
            var existing = _rows.FirstOrDefault(r => !r.IsCurrentUser
                && (string.Equals(r.Input, text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Name, text, StringComparison.CurrentCultureIgnoreCase)));

            if (existing is { IsSelected: true })
            {
                AddBox.Clear();
                return;
            }

            if (SelectedFriends >= MaxFriends)
            {
                AddMessageText.Text = $"Можна обрати не більше {MaxFriends} гравців, крім тебе.";
                AddMessageText.Visibility = Visibility.Visible;
                return;
            }

            if (existing is not null)
            {
                existing.IsSelected = true;
            }
            else
            {
                // SteamID тут невідомий: сервіс розпізнає ввід (SteamID64, посилання, нік) і поверне, хто це
                var row = new ParticipantRow(text, text, "", FriendStatus.AvatarBrush(text), FriendStatus.Initial(text), false, null)
                {
                    IsSelected = true
                };
                AddRow(row);
                UpdateSelectionState();
                // Новий гравець додається в кінець списку — показуємо його, а не лишаємо за межею прокрутки
                ParticipantsScroll.ScrollToEnd();
            }

            AddBox.Clear();
        }

        // ---------- пошук ----------

        private async void Find_Click(object sender, RoutedEventArgs e) => await SearchAsync();

        private async Task SearchAsync()
        {
            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;
            _hasSearched = true;

            var inputs = _rows.Where(r => !r.IsCurrentUser && r.IsSelected).Select(r => r.Input).ToList();

            foreach (var row in _rows)
            {
                row.LibraryHidden = false;
            }
            ClearBanners();
            ShowState(StateKind.Loading, "Порівнюємо бібліотеки…", "Запити йдуть паралельно, інтерфейс не блокується.");
            UpdateSelectionState();

            try
            {
                var result = await _matchmaker.FindCommonGamesAsync(inputs, cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (result.Status != DataStatus.Available || result.Value is null)
                {
                    ShowError(result.Message);
                    return;
                }

                var match = result.Value;
                ApplyHiddenFlags(match);
                ShowBanners(match);

                if (match.CommonGames.Count == 0)
                {
                    ShowState(StateKind.Empty, "Спільних ігор не знайдено",
                        result.Message ?? "У вибраних учасників немає жодної спільної гри. Спробуй змінити склад.",
                        "Змінити учасників", ShowHint);
                    return;
                }

                RenderGames(match);
                StatePanel.Hide();
                HintText.Visibility = Visibility.Collapsed;
                ResultCard.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Скасовано: пішли з екрана або запущено новий пошук
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowError(null);
                }
            }
            finally
            {
                if (_searchCts == cts)
                {
                    _searchCts = null;
                    UpdateSelectionState();
                }
                cts.Dispose();
            }
        }

        private void ApplyHiddenFlags(MatchResult match)
        {
            var hiddenIds = match.HiddenLibraryParticipants.Select(p => p.SteamId).ToHashSet();
            foreach (var row in _rows.Where(r => r.SteamId is not null))
            {
                row.LibraryHidden = hiddenIds.Contains(row.SteamId!);
            }
        }

        private void ShowBanners(MatchResult match)
        {
            if (match.HiddenLibraryParticipants.Count > 0)
            {
                HiddenBannerText.Text = MatchmakerFormat.HiddenLibraries(
                    match.HiddenLibraryParticipants.Select(p => p.PersonaName).ToList());
                HiddenBanner.Visibility = Visibility.Visible;
            }

            if (match.InvalidInputs.Count > 0)
            {
                InvalidBannerText.Text = string.Join("\n",
                    match.InvalidInputs.Select(i => $"Не вдалося додати «{i.Input}»: {i.Message}"));
                InvalidBanner.Visibility = Visibility.Visible;
            }
        }

        private void ClearBanners()
        {
            HiddenBanner.Visibility = Visibility.Collapsed;
            InvalidBanner.Visibility = Visibility.Collapsed;
        }

        private void RenderGames(MatchResult match)
        {
            var names = match.Participants.ToDictionary(p => p.SteamId, p => p.PersonaName);
            // Скільки учасників реально порівнювалось: приховані бібліотеки в перетині не беруть участі
            int counted = match.Participants.Count - match.HiddenLibraryParticipants.Count;
            decimal max = match.CommonGames.Max(g => g.AveragePlaytimeMinutes);

            ResultTitleText.Text = $"Спільні ігри · {match.CommonGames.Count}";
            GamesList.ItemsSource = match.CommonGames
                .Select((g, i) =>
                {
                    // Смуга — частка від найпопулярнішої гри списку
                    double fill = max > 0 ? (double)(g.AveragePlaytimeMinutes / max) : 0;
                    string tooltip = string.Join("\n", g.PlaytimeMinutesBySteamId.Select(p =>
                        $"{names.GetValueOrDefault(p.Key, p.Key)} — {LibraryFormat.Hours(p.Value)}"));

                    return new CommonGameRow(
                        i + 1,
                        g.Name,
                        g.IconUrl,
                        $"Є у всіх {counted} врахованих учасників",
                        new GridLength(fill, GridUnitType.Star),
                        new GridLength(1 - fill, GridUnitType.Star),
                        LibraryFormat.TotalHours((long)Math.Round(g.AveragePlaytimeMinutes)),
                        tooltip);
                })
                .ToList();
        }

        // ---------- стани правої частини ----------

        private void ShowHint()
        {
            ClearBanners();
            StatePanel.Hide();
            ResultCard.Visibility = Visibility.Collapsed;
            HintText.Visibility = Visibility.Visible;
        }

        private void ShowError(string? message) =>
            ShowState(StateKind.Error, "Не вдалося підібрати ігри",
                message ?? "Немає інтернету або Steam не відповідає. Застосунок працює далі.",
                "Повторити", Retry);

        private void ShowState(StateKind kind, string title, string message, string? actionText = null, Action? action = null)
        {
            ResultCard.Visibility = Visibility.Collapsed;
            HintText.Visibility = Visibility.Collapsed;
            StatePanel.Show(kind, title, message, actionText, action);
        }

        private async void Retry() => await SearchAsync();

        private async void FakeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isReady || _matchmaker is not FakeMatchmakerService fake || e.OriginalSource is not RadioButton { Tag: string mode })
            {
                return;
            }

            fake.ReturnEmpty = mode == "Empty";
            fake.Status = mode == "Error" ? DataStatus.Error : DataStatus.Available;

            if (_hasSearched && FindButton.IsEnabled)
            {
                await SearchAsync();
            }
        }
    }
}
