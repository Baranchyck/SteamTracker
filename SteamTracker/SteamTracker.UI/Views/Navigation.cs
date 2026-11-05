using System.Windows;

namespace SteamTracker.UI.Views
{
    public delegate void NavigationRequestedEventHandler(object sender, NavigationRequestedEventArgs e);

    /// <summary>Аргументи події переходу: Tag екрана, як у пункті меню MainWindow.</summary>
    public sealed class NavigationRequestedEventArgs : RoutedEventArgs
    {
        public NavigationRequestedEventArgs(RoutedEvent routedEvent, object source, string screen)
            : base(routedEvent, source)
        {
            Screen = screen;
        }

        public string Screen { get; }
    }

    /// <summary>
    /// Перехід на інший екран зсередини екрана. Подія спливає вгору по дереву до MainWindow,
    /// тож екрану не потрібно знати ні про вікно, ні про меню.
    /// </summary>
    public static class Navigation
    {
        public static readonly RoutedEvent RequestedEvent = EventManager.RegisterRoutedEvent(
            "Requested", RoutingStrategy.Bubble, typeof(NavigationRequestedEventHandler), typeof(Navigation));

        public static void Request(UIElement source, string screen) =>
            source.RaiseEvent(new NavigationRequestedEventArgs(RequestedEvent, source, screen));
    }
}
