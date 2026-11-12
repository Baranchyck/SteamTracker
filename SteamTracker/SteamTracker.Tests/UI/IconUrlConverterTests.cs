using System.Globalization;
using System.Windows.Media.Imaging;
using SteamTracker.UI.Converters;

namespace SteamTracker.Tests.UI;

public class IconUrlConverterTests
{
    private readonly IconUrlConverter _converter = new();

    private object? Convert(object? value) =>
        _converter.Convert(value!, typeof(object), null!, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/icon.png")]
    [InlineData("icon.png")]
    public void Convert_EmptyOrInvalidUrl_GivesNullInsteadOfThrowing(string? url) =>
        Assert.Null(Convert(url));

    [Fact]
    public void Convert_NotAString_GivesNull() =>
        Assert.Null(Convert(42));

    [Theory]
    [InlineData("file:///C:/definitely/missing/icon.png")]   // локальний файл, якого немає: не падаємо
    [InlineData("ftp://example.invalid/icon.png")]
    [InlineData("javascript:alert(1)")]
    public void Convert_NotHttpUrl_GivesNull(string url) =>
        Assert.Null(Convert(url));

    [Fact]
    public void Convert_HttpsUrl_GivesBitmapWithThatSource()
    {
        // .invalid гарантовано не резолвиться: картинка просто не завантажиться, а заглушка лишиться
        var image = Assert.IsType<BitmapImage>(Convert("https://icons.example.invalid/icon.png"));

        Assert.Equal(new Uri("https://icons.example.invalid/icon.png"), image.UriSource);
    }

    [Fact]
    public void ConvertBack_NotSupported() =>
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("x", typeof(string), null!, CultureInfo.InvariantCulture));
}
