using System.Text.RegularExpressions;
using SteamTracker.Domain.Exceptions; 

namespace SteamTracker.Integrations.Auth;

internal static class SteamOpenIdValidator
{
    private const string OpEndpoint = "https://steamcommunity.com/openid/login";

    private static readonly Regex ClaimedIdRegex =
        new(@"^https://steamcommunity\.com/openid/id/(\d{17})$", RegexOptions.Compiled);

    public static string ExtractSteamId(
        IReadOnlyDictionary<string, string> parameters,
        string expectedReturnTo)
    {
        Expect(parameters, "openid.ns", SteamOpenIdUrl.OpenIdNs);
        Expect(parameters, "openid.mode", "id_res");
        Expect(parameters, "openid.op_endpoint", OpEndpoint);
        Expect(parameters, "openid.return_to", expectedReturnTo);

        var claimedId = Require(parameters, "openid.claimed_id");
        var match = ClaimedIdRegex.Match(claimedId);
        if (!match.Success)
            throw new AuthFailedException($"Invalid claimed_id: {claimedId}");

        return match.Groups[1].Value;
    }

    private static string Require(IReadOnlyDictionary<string, string> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            throw new AuthFailedException($"Missing parameter: {key}");
        return value;
    }

    private static void Expect(IReadOnlyDictionary<string, string> parameters, string key, string expected)
    {
        var actual = Require(parameters, key);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new AuthFailedException($"Unexpected {key}: {actual}");
    }
}