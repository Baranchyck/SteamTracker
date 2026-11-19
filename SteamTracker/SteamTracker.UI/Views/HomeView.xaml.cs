using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Views.FriendsRadar;
using SteamTracker.UI.Views.Home;
using SteamTracker.UI.Views.Library;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Головний екран за макетом «02 · Головна»: привітання, швидка статистика, нещодавні ігри,
    /// друзі зараз і картки модулів. Бібліотека й друзі вантажаться паралельно й незалежно:
    /// помилка одного джерела показується банером лише про нього, решта екрана працює.
    /// </summary>
    public partial class HomeView : UserControl
    {
        private const int MaxRecentGames = 3;
        private const int MaxFriendsNow = 4;

        private enum BannerKind { Error, Warning }

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
            GreetingText.Text = HomeFormat.Greeting(_auth.CurrentUser?.PersonaName);
            await RefreshAllAsync();
        }

        /// <summary>«Оновити дані»: обидва блоки разом; кнопка вимкнена, поки йдуть запити.</summary>
        private async Task RefreshAllAsync()
        {
            RefreshButton.IsEnabled = false;
            try
            {
                await Task.WhenAll(LoadLibraryAsync(), LoadFriendsAsync());
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

        // ---------- бібліотека ----------

        private async Task LoadLibraryAsync()
        {
            _libraryCts?.Cancel();
            var cts = new CancellationTokenSource();
            _libraryCts = cts;

            ShowLibraryLoading();

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
                        ShowLibraryUnavailable(BannerKind.Warning,
                            "Бібліотека прихована. Відкрий: Steam → Профіль → Налаштування приватності → Game details → Public.",
                            "Перевірити знову");
                        return;
                    case DataStatus.Error:
                        ShowLibraryUnavailable(BannerKind.Error,
                            "Не вдалося отримати бібліотеку: немає інтернету або Steam не відповідає.", "Повторити");
                        return;
                }

                RenderLibrary(result.Value ?? Array.Empty<OwnedGame>());
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
                    ShowLibraryUnavailable(BannerKind.Error,
                        "Не вдалося отримати бібліотеку: немає інтернету або Steam не відповідає.", "Повторити");
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

        private void ShowLibraryLoading()
        {
            LibraryBanner.Visibility = Visibility.Collapsed;
            GamesCountText.Text = "…";
            GamesCaptionText.Text = "Завантажуємо бібліотеку…";
            TotalHoursText.Text = "…";
            RecentList.ItemsSource = null;
            ShowRecentStatus("Завантажуємо…");
        }

        private void RenderLibrary(IReadOnlyList<OwnedGame> games)
        {
            LibraryBanner.Visibility = Visibility.Collapsed;

            int barelyPlayed = HomeFormat.BarelyPlayedCount(games);
            GamesCountText.Text = LibraryFormat.Count(games.Count);
            GamesCaptionText.Text = games.Count == 0 ? "у бібліотеці поки немає ігор" : HomeFormat.BarelyPlayedCaption(barelyPlayed);
            TotalHoursText.Text = LibraryFormat.TotalHours(games.Sum(g => (long)g.PlaytimeForeverMinutes));
            LibraryModuleText.Text = HomeFormat.WaitingForLaunch(barelyPlayed);

            var recent = HomeFormat.RecentlyPlayed(games, MaxRecentGames)
                .Select(g => new RecentGameRow(
                    g.Name,
                    g.IconUrl,
                    HomeFormat.TotalPlaytime(g.PlaytimeForeverMinutes),
                    HomeFormat.RecentPlaytime(g.Playtime2WeeksMinutes)))
                .ToList();

            RecentList.ItemsSource = recent;
            if (recent.Count > 0)
            {
                RecentStatusText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowRecentStatus(games.Count == 0
                    ? "У бібліотеці немає ігор."
                    : "За останні 2 тижні ти не запускав жодної гри.");
            }
        }

        /// <summary>Бібліотека недоступна: банер з поясненням і кнопкою, у картках — прочерки.</summary>
        private void ShowLibraryUnavailable(BannerKind kind, string message, string action)
        {
            ShowBanner(LibraryBanner, LibraryBannerDot, LibraryBannerText, LibraryBannerAction, kind, message, action);

            GamesCountText.Text = "—";
            GamesCaptionText.Text = "бібліотека недоступна";
            TotalHoursText.Text = "—";
            LibraryModuleText.Text = "Усі ігри з сортуванням і фільтрами";
            RecentList.ItemsSource = null;
            ShowRecentStatus("Дані бібліотеки недоступні.");
        }

        private void ShowRecentStatus(string text)
        {
            RecentStatusText.Text = text;
            RecentStatusText.Visibility = Visibility.Visible;
        }

        private async void LibraryBannerAction_Click(object sender, RoutedEventArgs e) => await LoadLibraryAsync();

        // ---------- друзі ----------

        private async Task LoadFriendsAsync()
        {
            _friendsCts?.Cancel();
            var cts = new CancellationTokenSource();
            _friendsCts = cts;

            ShowFriendsLoading();

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
                        ShowFriendsUnavailable(BannerKind.Warning,
                            "Список друзів прихований. Відкрий: Steam → Профіль → Налаштування приватності → Friends list → Public.",
                            "Перевірити знову");
                        return;
                    case DataStatus.Error:
                        ShowFriendsUnavailable(BannerKind.Error,
                            "Не вдалося отримати друзів: немає інтернету або Steam не відповідає.", "Повторити");
                        return;
                }

                RenderFriends(result.Value ?? Array.Empty<PlayerPresence>());
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
                    ShowFriendsUnavailable(BannerKind.Error,
                        "Не вдалося отримати друзів: немає інтернету або Steam не відповідає.", "Повторити");
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

        private void ShowFriendsLoading()
        {
            FriendsBanner.Visibility = Visibility.Collapsed;
            FriendsOnlineText.Text = "…";
            FriendsCaptionText.Text = "Шукаємо друзів онлайн…";
            FriendsNowList.ItemsSource = null;
            ShowFriendsNowStatus("Завантажуємо…");
        }

        private void RenderFriends(IReadOnlyList<PlayerPresence> friends)
        {
            FriendsBanner.Visibility = Visibility.Collapsed;

            // Сервіс уже впорядкував: у грі → онлайн → відійшли → офлайн, тож перші N — найактивніші
            var inSteam = friends.Where(HomeFormat.IsInSteam).ToList();
            int inGame = inSteam.Count(FriendStatus.IsInGame);

            FriendsOnlineText.Text = HomeFormat.OfTotal(inSteam.Count, friends.Count);
            FriendsCaptionText.Text = friends.Count == 0
                ? "друзів у Steam поки немає"
                : HomeFormat.FriendsBreakdown(inSteam.Count, inGame);

            var rows = inSteam
                .Take(MaxFriendsNow)
                .Select(f => new FriendNowRow(
                    f.PersonaName,
                    FriendStatus.Initial(f.PersonaName),
                    f.AvatarUrl,
                    FriendStatus.AvatarBrush(f.SteamId),
                    HomeFormat.StatusLine(f),
                    (Brush)FindResource(FriendStatus.BrushKey(FriendStatus.GroupOf(f)))))
                .ToList();

            FriendsNowList.ItemsSource = rows;
            if (rows.Count > 0)
            {
                FriendsNowStatusText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowFriendsNowStatus(friends.Count == 0
                    ? "Друзів у Steam поки немає."
                    : "Зараз ніхто з друзів не в мережі.");
            }
        }

        /// <summary>Друзі недоступні: банер з поясненням і кнопкою, у картках — прочерки.</summary>
        private void ShowFriendsUnavailable(BannerKind kind, string message, string action)
        {
            ShowBanner(FriendsBanner, FriendsBannerDot, FriendsBannerText, FriendsBannerAction, kind, message, action);

            FriendsOnlineText.Text = "—";
            FriendsCaptionText.Text = "дані про друзів недоступні";
            FriendsNowList.ItemsSource = null;
            ShowFriendsNowStatus("Дані про друзів недоступні.");
        }

        private void ShowFriendsNowStatus(string text)
        {
            FriendsNowStatusText.Text = text;
            FriendsNowStatusText.Visibility = Visibility.Visible;
        }

        private async void FriendsBannerAction_Click(object sender, RoutedEventArgs e) => await LoadFriendsAsync();

        // ---------- банери ----------

        /// <summary>Банер стану, як на матчмейкері: червоний — помилка, жовтий — приховані дані.</summary>
        private void ShowBanner(Border banner, Ellipse dot, TextBlock text, Button action,
            BannerKind kind, string message, string actionText)
        {
            var foreground = (Brush)FindResource(kind == BannerKind.Error ? "Danger" : "Warning");

            banner.Background = (Brush)FindResource(kind == BannerKind.Error ? "DangerSoft" : "WarningSoft");
            dot.Fill = foreground;
            text.Foreground = foreground;
            text.Text = message;
            action.Content = actionText + " →";
            banner.Visibility = Visibility.Visible;
        }

        // ---------- переходи ----------

        private void OpenRadarButton_Click(object sender, RoutedEventArgs e) => Navigation.Request(this, "FriendsRadar");

        /// <summary>Клік по будь-якій картці модуля: Tag картки — ключ екрана, як у пункті меню.</summary>
        private void ModuleCard_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button { Tag: string screen })
            {
                Navigation.Request(this, screen);
            }
        }
    }
}
