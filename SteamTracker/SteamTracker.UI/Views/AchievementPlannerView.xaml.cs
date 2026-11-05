using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.UI.Controls;
using SteamTracker.UI.Fakes;
using SteamTracker.UI.Views.Achievements;

namespace SteamTracker.UI.Views
{
    /// <summary>
    /// Екран «Досягнення»: прогрес обраної гри, калькулятор цілі (+N пунктів) і незакриті ачівки від найлегших.
    /// </summary>
    public partial class AchievementPlannerView : UserControl
    {
        private const double BarMaxWidth = 160;

        private readonly IAchievementService _achievements;
        private CancellationTokenSource? _gamesCts;
        private CancellationTokenSource? _planCts;

        // План обраної гри; калькулятор цілі рахує по ньому без запитів у мережу
        private AchievementPlan? _plan;
        private HashSet<string> _roadmapIds = new();

        // XAML виставляє початкові значення (Text, IsSelected) ще в InitializeComponent,
        // і їхні обробники не повинні нічого рахувати до того, як екран готовий
        private readonly bool _isReady;

        public AchievementPlannerView(IAchievementService achievements)
        {
            _achievements = achievements;
            InitializeComponent();

#if DEBUG
            if (achievements is FakeAchievementService)
            {
                FakeModePanel.Visibility = Visibility.Visible;
            }
#endif

            _isReady = true;
            Loaded += OnLoaded;
            // Користувач пішов з екрана — запити більше не потрібні
            Unloaded += (_, _) =>
            {
                _gamesCts?.Cancel();
                _planCts?.Cancel();
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadGamesAsync();

        private async Task LoadGamesAsync()
        {
            _gamesCts?.Cancel();
            _planCts?.Cancel();
            var cts = new CancellationTokenSource();
            _gamesCts = cts;

            GameBox.Visibility = Visibility.Collapsed;
            ShowState(StateKind.Loading, "Завантажуємо досягнення…",
                "Шукаємо ігри, де закрито частину досягнень. Бічне меню вже доступне.");

            try
            {
                var result = await _achievements.GetGamesInProgressAsync(cts.Token);
                if (cts.IsCancellationRequested || ShowFailureIfAny(result.Status))
                {
                    return;
                }

                var games = result.Value ?? Array.Empty<AchievementGameProgress>();
                if (games.Count == 0)
                {
                    ShowState(StateKind.Empty, "Немає ігор у процесі",
                        "Тут з’являються ігри, де закрито хоча б одне, але не всі досягнення.",
                        "Оновити", Reload);
                    return;
                }

                GameBox.ItemsSource = games
                    .Select(g => new GameOption(g.AppId, $"{g.Name} · {g.Achieved}/{g.Total}"))
                    .ToList();
                GameBox.Visibility = Visibility.Visible;
                // Вибір першої гри запускає завантаження її плану через Game_Changed
                GameBox.SelectedIndex = 0;
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
                if (_gamesCts == cts)
                {
                    _gamesCts = null;
                }
                cts.Dispose();
            }
        }

        private async Task LoadPlanAsync(int appId)
        {
            // План попередньо обраної гри вже неактуальний
            _planCts?.Cancel();
            var cts = new CancellationTokenSource();
            _planCts = cts;

            if (ContentPanel.Visibility == Visibility.Visible)
            {
                ContentPanel.Opacity = 0.5;
            }
            else
            {
                ShowState(StateKind.Loading, "Завантажуємо план гри…", "Рахуємо, які досягнення закрити найпростіше.");
            }

            try
            {
                var result = await _achievements.GetPlanAsync(appId, cts.Token);
                if (cts.IsCancellationRequested || ShowFailureIfAny(result.Status))
                {
                    return;
                }

                _plan = result.Value ?? new AchievementPlan(appId, 0, 0, Array.Empty<PlannedAchievement>());
                ClampTarget();
                RenderProgress();
                UpdateRoadmap();

                StatePanel.Hide();
                ContentPanel.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Скасовано: обрали іншу гру або пішли з екрана
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
                if (_planCts == cts)
                {
                    _planCts = null;
                    ContentPanel.Opacity = 1;
                }
                cts.Dispose();
            }
        }

        private void RenderProgress()
        {
            var plan = _plan!;
            double percent = (double)plan.Percent;

            ProgressPercentText.Text = AchievementFormat.ProgressPercent(plan.Percent);
            ProgressCountText.Text = AchievementFormat.Closed(plan.Achieved, plan.Total);
            ProgressDoneColumn.Width = new GridLength(percent, GridUnitType.Star);
            ProgressLeftColumn.Width = new GridLength(100 - percent, GridUnitType.Star);
            RemainingBadgeText.Text = plan.Remaining.Count.ToString();
        }

        // Ціль, яка лишилась з попередньої гри, могла стати недосяжною: зменшуємо до максимуму для нової
        private void ClampTarget()
        {
            int maxPoints = (int)Math.Floor(100 - _plan!.Percent);
            if (maxPoints >= 1 && int.TryParse(TargetBox.Text, out int points) && points > maxPoints)
            {
                TargetBox.Text = maxPoints.ToString();
            }
        }

        // Калькулятор цілі: рахує по вже завантаженому плану, без запитів у мережу
        private void UpdateRoadmap()
        {
            var plan = _plan;
            if (plan is null)
            {
                return;
            }

            RoadmapBanner.Visibility = Visibility.Collapsed;
            _roadmapIds = new HashSet<string>();

            if (plan.Remaining.Count == 0)
            {
                TargetBox.IsEnabled = false;
                ShowRoadmapHint("Усі досягнення закрито — прогрес 100%.", isError: false);
                RenderRows();
                return;
            }

            TargetBox.IsEnabled = true;

            // Підняти можна щонайбільше до 100%
            int maxPoints = (int)Math.Floor(100 - plan.Percent);
            if (maxPoints < 1)
            {
                _roadmapIds = plan.Remaining.Select(a => a.ApiName).ToHashSet();
                ShowRoadmapHint($"До 100% лишилось менше одного пункту: закрий останні {AchievementFormat.Achievements(plan.Remaining.Count)}.",
                    isError: false);
                RenderRows();
                return;
            }

            if (!int.TryParse(TargetBox.Text, out int points) || points < 1 || points > maxPoints)
            {
                ShowRoadmapHint($"Введи число від 1 до {maxPoints}.", isError: true);
                RenderRows();
                return;
            }

            var roadmap = _achievements.BuildRoadmap(plan, points);
            _roadmapIds = roadmap.Achievements.Select(a => a.ApiName).ToHashSet();

            RoadmapCountRun.Text = "Потрібно закрити " + AchievementFormat.Achievements(roadmap.Achievements.Count);
            RoadmapResultRun.Text = $"→ {AchievementFormat.Percent(roadmap.NewPercent)} " +
                                    $"({plan.Achieved + roadmap.Achievements.Count} з {plan.Total})";
            RoadmapBanner.Visibility = Visibility.Visible;
            RoadmapHintText.Visibility = Visibility.Collapsed;

            RenderRows();
        }

        private void RenderRows()
        {
            var remaining = _plan?.Remaining ?? Array.Empty<PlannedAchievement>();

            // Сервіс уже віддає від найлегших; «від найрідкісніших» — навпаки, без % лишаються в кінці
            var ordered = SortBox.SelectedItem is ComboBoxItem { Tag: "Rarest" }
                ? remaining.OrderBy(a => a.GlobalPercent is null).ThenBy(a => a.GlobalPercent).ToList()
                : remaining.ToList();

            AchievementsList.ItemsSource = ordered
                .Select(a => new AchievementRow(
                    a.DisplayName,
                    a.Description ?? "Опис прихований",
                    AchievementFormat.GlobalPercent(a.GlobalPercent),
                    BarWidth(a.GlobalPercent),
                    BarBrush(a.GlobalPercent),
                    _roadmapIds.Contains(a.ApiName)))
                .ToList();

            AllDoneText.Visibility = remaining.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Ненульовий % — щонайменше крапка (4 px), щоб рідкісні ачівки теж мали смужку
        private static double BarWidth(double? percent) =>
            percent is double value && value > 0 ? Math.Max(4, BarMaxWidth * value / 100) : 0;

        // Як у макеті: поширені — зелені, середні — сині, рідкісні — жовті
        private Brush BarBrush(double? percent) => (Brush)FindResource(percent switch
        {
            >= 50 => "Success",
            >= 10 => "Accent",
            _ => "Warning"
        });

        private void ShowRoadmapHint(string text, bool isError)
        {
            RoadmapHintText.Text = text;
            RoadmapHintText.Foreground = (Brush)FindResource(isError ? "Danger" : "TextSecondary");
            RoadmapHintText.Visibility = Visibility.Visible;
        }

        private bool ShowFailureIfAny(DataStatus status)
        {
            switch (status)
            {
                case DataStatus.Private:
                    ShowState(StateKind.Private, "Профіль приватний",
                        "Steam не віддає досягнення. Відкрий: Steam → Профіль → Налаштування приватності → Game details → Public.",
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

        private async void Reload() => await LoadGamesAsync();

        private async void Game_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady && GameBox.SelectedItem is GameOption game)
            {
                await LoadPlanAsync(game.AppId);
            }
        }

        private void Sort_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady && _plan is not null)
            {
                RenderRows();
            }
        }

        private void Target_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isReady)
            {
                UpdateRoadmap();
            }
        }

        // У поле можна ввести лише цифри
        private void Target_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !e.Text.All(char.IsDigit);

        private async void FakeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isReady || _achievements is not FakeAchievementService fake || e.OriginalSource is not RadioButton { Tag: string mode })
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

            await LoadGamesAsync();
        }
    }
}
