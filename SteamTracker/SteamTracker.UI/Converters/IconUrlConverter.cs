using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SteamTracker.UI.Converters
{
    /// <summary>
    /// URL іконки гри → картинка. BitmapImage з http-адресою качається у фоні, тож UI не блокується,
    /// а поки картинки немає (чи вона не завантажилась), видно заглушку під нею.
    /// Разом із віртуалізацією списку качаються лише іконки видимих рядків.
    /// </summary>
    public sealed class IconUrlConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.DecodePixelWidth = 96; // обкладинка 48 px, ×2 для чіткості на HiDPI
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();
            return image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
