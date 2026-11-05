using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace SteamTracker.UI.Views
{
    /// <summary>Екран, зареєстрований у DI, і Tag пункту меню, який його відкриває.</summary>
    public sealed record ScreenRegistration(string Tag, Type ViewType);

    /// <summary>
    /// Створює екран за Tag пункту меню через DI-контейнер: кожен екран сам отримує свої сервіси через конструктор.
    /// </summary>
    public sealed class ViewFactory
    {
        private readonly IServiceProvider _services;
        private readonly IReadOnlyDictionary<string, Type> _screens;

        public ViewFactory(IServiceProvider services, IEnumerable<ScreenRegistration> screens)
        {
            _services = services;
            _screens = screens.ToDictionary(s => s.Tag, s => s.ViewType);
        }

        public UserControl Create(string tag) =>
            _screens.TryGetValue(tag, out var viewType)
                ? (UserControl)_services.GetRequiredService(viewType)
                : throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unknown screen");
    }
}
