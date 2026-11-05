using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Views.Home;
using SteamTracker.UI.Views.Library;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Головний екран: вік акаунту, друзі онлайн і коротка статистика бібліотеки.
    /// Блоки вантажаться паралельно й незалежно: помилка одного джерела показується лише в його блоці.
    /// </summary>
    public partial class HomeView : UserControl
    {
        private readonly ILibraryService _library;
        private readonly IAuthService _auth;
        private readonly IFriendsRadarService _friends;

        // Окремий токен на кожен блок: «Повторити» в одному не скасовує завантаження іншого
        private CancellationTokenSource? _libraryCts;
        private CancellationTokenSource? _friendsCts;

        public HomeView(ILibraryService library, IAuthService auth, IFriendsRadarService friends)
        {
            _library = library;
            _auth = auth;
            _friends = friends;
            InitializeComponent();

            Loaded += OnLoaded;
            // Користувач пішов з екрана — запити більше не потрібні
            Unloaded += (_, _) =>
            {
                _libraryCts?.Cancel();
                _friendsCts?.Cancel();
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ShowAccount(_auth.CurrentUser);
            await Task.WhenAll(LoadLibraryAsync(), LoadFriendsAsync());
        }

        // ---------- акаунт ----------

        private void ShowAccount(SteamProfile? user)
        {
            if (user?.TimeCreated is not { } created)
            {
                AccountAgeText.Text = "—";
                AccountSinceText.Text = "Дата реєстрації недоступна: профіль приватний або ще не оновлювався після входу.";
                AccountBadgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            int years = HomeFormat.FullYears(created, DateTimeOffset.Now);
            AccountAgeText.Text = HomeFormat.Years(years);
            AccountSinceText.Text = HomeFormat.Since(created);

            var badge = HomeFormat.Badge(years);
            AccountBadgeBorder.Visibility = badge == AccountBadge.None ? Visibility.Collapsed : Visibility.Visible;
            AccountBadgeText.Text = badge == AccountBadge.Veteran ? "Ветеран" : "Новачок";
            AccountBadgeText.Foreground = (Brush)FindResource(badge == AccountBadge.Veteran ? "Success" : "Accent");
        }

        // ---------- бібліотека ----------

        private async Task LoadLibraryAsync()
        {
            _libraryCts?.Cancel();
            var cts = new CancellationTokenSource();
            _libraryCts = cts;

            LibraryCards.Visibility = Visibility.Collapsed;
            LibraryState.Show(StateKind.Loading, "Завантажуємо бібліотеку…", "Статистика з’явиться за кілька секунд.");

            try
            {
                var result = await _library.GetMyLibraryAsync(new LibraryFilter(), cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                switch (result.Status)
                {
                    case DataStatus.Private:
                        LibraryState.Show(StateKind.Private, "Бібліотека прихована",
                            "Steam не віддає список ігор. Відкрий: Steam → Профіль → Налаштування приватності → Game details → Public.",
                            "Перевірити знову", ReloadLibrary);
                        return;
                    case DataStatus.Error:
                        ShowLibraryError();
                        return;
                }

                var games = result.Value ?? Array.Empty<OwnedGame>();
                if (games.Count == 0)
                {
                    LibraryState.Show(StateKind.Empty, "Бібліотека порожня",
                        "На акаунті ще немає ігор.", "Оновити", ReloadLibrary);
                    return;
                }

                RenderLibrary(games);
                LibraryState.Hide();
                LibraryCards.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Пішли з екрана або вже запущено нове завантаження
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowLibraryError();
                }
            }
            finally
            {
                if (_libraryCts == cts)
                {
                    _libraryCts = null;
                }
                cts.Dispose();
            }
        }

        private void RenderLibrary(IReadOnlyList<OwnedGame> games)
        {
            GamesCountText.Text = LibraryFormat.Count(games.Count);
            TotalHoursText.Text = LibraryFormat.TotalHours(games.Sum(g => (long)g.PlaytimeForeverMinutes));
            NeverPlayedText.Text = LibraryFormat.Count(games.Count(g => g.PlaytimeForeverMinutes == 0));

            // Серед ігор, які хоч раз запускались; ніколи не запущені рахує сусідня картка
            var oldest = games
                .Where(g => g.LastPlayed is not null)
                .MinBy(g => g.LastPlayed);

            if (oldest is null)
            {
                OldestGameText.Text = "—";
                OldestGameCaption.Text = "Жодна гра ще не запускалась";
            }
            else
            {
                OldestGameText.Text = oldest.Name;
                OldestGameText.ToolTip = oldest.Name;
                OldestGameCaption.Text = LibraryFormat.LastPlayed(oldest.LastPlayed);
            }
        }

        private void ShowLibraryError()
        {
            LibraryCards.Visibility = Visibility.Collapsed;
            LibraryState.Show(StateKind.Error, "Не вдалося отримати бібліотеку",
                "Немає інтернету або Steam не відповідає. Решта екрана працює далі.",
                "Повторити", ReloadLibrary);
        }

        private async void ReloadLibrary() => await LoadLibraryAsync();

        // ---------- друзі ----------

        private async Task LoadFriendsAsync()
        {
            _friendsCts?.Cancel();
            var cts = new CancellationTokenSource();
            _friendsCts = cts;

            FriendsCard.Visibility = Visibility.Collapsed;
            FriendsState.Show(StateKind.Loading, "Шукаємо друзів онлайн…", "Це займе кілька секунд.");

            try
            {
                var result = await _friends.GetFriendsAsync(new RadarFilter(), cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                switch (result.Status)
                {
                    case DataStatus.Private:
                        FriendsState.Show(StateKind.Private, "Список друзів прихований",
                            "Відкрий: Steam → Профіль → Налаштування приватності → Friends list → Public.",
                            "Перевірити знову", ReloadFriends);
                        return;
                    case DataStatus.Error:
                        ShowFriendsError();
                        return;
                }

                RenderFriends(result.Value ?? Array.Empty<PlayerPresence>());
                FriendsState.Hide();
                FriendsCard.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Пішли з екрана або вже запущено нове завантаження
            }
            catch (Exception)
            {
                // TODO: залогувати (коли з'явиться логер)
                if (!cts.IsCancellationRequested)
                {
                    ShowFriendsError();
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

        private void RenderFriends(IReadOnlyList<PlayerPresence> friends)
        {
            int online = friends.Count(f => f.PersonaState != PersonaState.Offline);
            // Назва гри є і для сторонніх (non-Steam) ігор, де CurrentGameId = null
            int inGame = friends.Count(f => f.CurrentGameName is not null);

            FriendsOnlineText.Text = LibraryFormat.Count(online);
            FriendsCaptionText.Text = friends.Count == 0
                ? "Друзів у Steam поки немає"
                : $"з {LibraryFormat.Count(friends.Count)} друзів · у грі: {LibraryFormat.Count(inGame)}";
        }

        private void ShowFriendsError()
        {
            FriendsCard.Visibility = Visibility.Collapsed;
            FriendsState.Show(StateKind.Error, "Не вдалося отримати друзів",
                "Немає інтернету або Steam не відповідає.", "Повторити", ReloadFriends);
        }

        private async void ReloadFriends() => await LoadFriendsAsync();

        // ---------- переходи ----------

        private void OpenRadarButton_Click(object sender, RoutedEventArgs e) => Navigation.Request(this, "FriendsRadar");

        private void OpenLibraryButton_Click(object sender, RoutedEventArgs e) => Navigation.Request(this, "Library");
    }
}
