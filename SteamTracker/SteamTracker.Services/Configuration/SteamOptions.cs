using Microsoft.Extensions.Configuration;
using SteamTracker.Domain.Exceptions;

namespace SteamTracker.Services.Configuration;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    // Значення з appsettings.example.json — вважаємо, що ключ не задано.
    public const string ApiKeyPlaceholder = "YOUR_STEAM_API_KEY";

    public string ApiKey { get; set; } = string.Empty;

    public static SteamOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<SteamOptions>() ?? new SteamOptions();

        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey == ApiKeyPlaceholder)
            throw new ConfigurationMissingException(
                $"Задай {SectionName}:ApiKey (user-secrets або appsettings.json), див. README.");

        return options;
    }
}
