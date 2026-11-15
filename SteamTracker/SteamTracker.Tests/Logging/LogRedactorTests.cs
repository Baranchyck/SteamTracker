using SteamTracker.UI.Logging;

namespace SteamTracker.Tests.Logging;

public class LogRedactorTests
{
    private const string ApiKey = "ABCDEF0123456789ABCDEF0123456789";

    [Fact]
    public void Redact_ConfiguredSecret_IsMaskedEverywhere()
    {
        var redactor = new LogRedactor(new[] { ApiKey });

        var result = redactor.Redact($"ключ {ApiKey}, ще раз {ApiKey}");

        Assert.DoesNotContain(ApiKey, result);
        Assert.Equal($"ключ {LogRedactor.Mask}, ще раз {LogRedactor.Mask}", result);
    }

    [Theory]
    [InlineData("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=SECRET123&steamids=1",
                "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=***&steamids=1")]
    [InlineData("GET /IPlayerService/GetOwnedGames/v1/?steamid=1&KEY=secret",
                "GET /IPlayerService/GetOwnedGames/v1/?steamid=1&KEY=***")]
    public void Redact_KeyInQueryString_IsMaskedEvenIfNotConfigured(string input, string expected)
    {
        var redactor = new LogRedactor(Array.Empty<string>());

        Assert.Equal(expected, redactor.Redact(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")] // коротке значення — це не ключ, маскування зіпсувало б текст
    public void Redact_EmptyOrShortSecret_IsIgnored(string? secret)
    {
        var redactor = new LogRedactor(new[] { secret });

        Assert.Equal("short message", redactor.Redact("short message"));
    }

    [Fact]
    public void Redact_TextWithoutSecrets_IsUnchanged()
    {
        var redactor = new LogRedactor(new[] { ApiKey });

        Assert.Equal("Library: бібліотека порожня", redactor.Redact("Library: бібліотека порожня"));
    }
}
