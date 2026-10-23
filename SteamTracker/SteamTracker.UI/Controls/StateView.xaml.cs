using System.Windows;
using System.Windows.Controls;

namespace SteamTracker.UI.Controls
{
    public enum StateKind { Loading, Empty, Error, Private }

    /// <summary>
    /// Картка стану вікна (завантаження / порожньо / помилка / приватний профіль).
    /// Тексти задає екран, бо для кожного вікна вони свої.
    /// </summary>
    public partial class StateView : UserControl
    {
        private Action? _action;

        public StateView()
        {
            InitializeComponent();
        }

        /// <param name="actionText">Текст кнопки; null — без кнопки.</param>
        /// <param name="action">Що зробити при натисканні кнопки.</param>
        public void Show(StateKind kind, string title, string message, string? actionText = null, Action? action = null)
        {
            Spinner.Visibility = VisibleIf(kind == StateKind.Loading);
            Skeleton.Visibility = VisibleIf(kind == StateKind.Loading);
            EmptyIcon.Visibility = VisibleIf(kind == StateKind.Empty);
            ErrorIcon.Visibility = VisibleIf(kind == StateKind.Error);
            PrivateIcon.Visibility = VisibleIf(kind == StateKind.Private);

            TitleText.Text = title;
            MessageText.Text = message;

            _action = action;
            ActionButton.Content = actionText;
            ActionButton.Visibility = VisibleIf(actionText is not null);
            // У макеті «порожньо» має нейтральну кнопку, помилка й приватний профіль — акцентну
            ActionButton.Style = (Style)FindResource(kind == StateKind.Empty ? "StateSubtleButton" : "StateButton");

            Visibility = Visibility.Visible;
        }

        public void Hide() => Visibility = Visibility.Collapsed;

        private void ActionButton_Click(object sender, RoutedEventArgs e) => _action?.Invoke();

        private static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    }
}
