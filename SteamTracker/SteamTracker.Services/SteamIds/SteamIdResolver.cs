using System.Text.RegularExpressions;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;

namespace SteamTracker.Services.SteamIds;

public class SteamIdResolver : ISteamIdResolver
{
    // Найдовше легітимне: https://www.steamcommunity.com/id/<32 символи>/ — близько 70.
    // Усе, що значно довше, — сміття, у Steam його не відправляємо.
    public const int MaxInputLength = 200;

    // Індивідуальні акаунти: universe 1, type 1 → SteamID64 = 7656119xxxxxxxxxx
    private const string IndividualPrefix = "7656119";
    private const int SteamId64Length = 17;

    private const string CommunityHost = "steamcommunity.com";

    // Custom URL у Steam: латиниця, цифри, '_' і '-', до 32 символів
    private static readonly Regex VanityPattern = new("^[A-Za-z0-9_-]{2,32}$", RegexOptions.Compiled);

    private readonly ISteamApiClient _steamApi;

    public SteamIdResolver(ISteamApiClient steamApi)
    {
        _steamApi = steamApi;
    }

    public async Task<SteamIdResolveResult> ResolveAsync(string input, CancellationToken ct = default)
    {
        var original = input ?? string.Empty;
        var text = original.Trim();

        if (text.Length == 0)
            return Fail(SteamIdResolveError.Empty, "Введіть SteamID, посилання на профіль або нік.");

        if (text.Length > MaxInputLength)
            return Fail(SteamIdResolveError.TooLong, "Занадто довгий ввід.");

        if (LooksLikeUrl(text))
            return await ResolveUrlAsync(text);

        if (text.All(char.IsAsciiDigit))
            return ValidateSteamId64(text);

        return await ResolveVanityAsync(text);

        SteamIdResolveResult Fail(SteamIdResolveError error, string message) =>
            SteamIdResolveResult.Fail(original, error, message);

        SteamIdResolveResult ValidateSteamId64(string id)
        {
            if (!IsSteamId64(id))
                return Fail(SteamIdResolveError.InvalidSteamId,
                    $"SteamID64 має складатися з {SteamId64Length} цифр і починатися з {IndividualPrefix}.");

            return SteamIdResolveResult.Ok(original, id);
        }

        async Task<SteamIdResolveResult> ResolveUrlAsync(string url)
        {
            var rest = StripPrefix(url, "https://") ?? StripPrefix(url, "http://") ?? url;
            if (rest.Contains("://", StringComparison.Ordinal))
                return Fail(SteamIdResolveError.InvalidFormat, "Непідтримуване посилання.");

            rest = StripPrefix(rest, "www.") ?? rest;

            // Query і fragment для профілю нічого не значать: ?l=ukrainian, #comments
            int tail = rest.IndexOfAny(new[] { '?', '#' });
            if (tail >= 0)
                rest = rest[..tail];

            int slash = rest.IndexOf('/');
            var host = slash >= 0 ? rest[..slash] : rest;
            if (!host.Equals(CommunityHost, StringComparison.OrdinalIgnoreCase))
                return Fail(SteamIdResolveError.ForeignHost, "Посилання має вести на steamcommunity.com.");

            var segments = slash >= 0
                ? rest[(slash + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            if (segments.Length != 2)
                return Fail(SteamIdResolveError.InvalidFormat,
                    "Очікується посилання виду steamcommunity.com/profiles/<id> або steamcommunity.com/id/<нік>.");

            return segments[0].ToLowerInvariant() switch
            {
                "profiles" => ValidateSteamId64(segments[1]),
                "id" => await ResolveVanityAsync(segments[1]),
                _ => Fail(SteamIdResolveError.InvalidFormat,
                    "Очікується посилання виду steamcommunity.com/profiles/<id> або steamcommunity.com/id/<нік>.")
            };
        }

        async Task<SteamIdResolveResult> ResolveVanityAsync(string vanity)
        {
            if (!VanityPattern.IsMatch(vanity))
                return Fail(SteamIdResolveError.InvalidFormat,
                    "Нік може містити лише латиницю, цифри, '_' і '-' (2–32 символи).");

            string? steamId;
            try
            {
                // Екранування — у клієнті (Uri.EscapeDataString), а шаблон вище вже відсікає зайве
                steamId = await _steamApi.ResolveVanityUrlAsync(vanity, ct);
            }
            catch (SteamApiException)
            {
                return Fail(SteamIdResolveError.SteamUnavailable, "Steam не відповідає, спробуйте пізніше.");
            }

            if (steamId is null)
                return Fail(SteamIdResolveError.NotFound, $"Профіль з ніком \"{vanity}\" не знайдено.");

            // Не довіряємо відповіді наосліп: далі цей id піде в інші запити
            if (!IsSteamId64(steamId))
                return Fail(SteamIdResolveError.SteamUnavailable, "Steam повернув некоректний SteamID.");

            return SteamIdResolveResult.Ok(original, steamId);
        }
    }

    public static bool IsSteamId64(string value) =>
        value.Length == SteamId64Length
        && value.StartsWith(IndividualPrefix, StringComparison.Ordinal)
        && value.All(char.IsAsciiDigit);

    private static bool LooksLikeUrl(string text) =>
        text.Contains('/') || text.Contains("://", StringComparison.Ordinal) || text.Contains('.');

    private static string? StripPrefix(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? text[prefix.Length..] : null;
}
