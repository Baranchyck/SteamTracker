using SteamTracker.Domain.Exceptions; 

namespace SteamTracker.Integrations.Auth;

internal sealed class SteamOpenIdVerifier
{
    private readonly HttpClient _http;

    public SteamOpenIdVerifier(HttpClient http)
    {
        _http = http;
    }

    public async Task VerifyAsync(
        IReadOnlyDictionary<string, string> callbackParameters,
        CancellationToken ct)
    {
        var form = callbackParameters.ToDictionary(p => p.Key, p => p.Value);
        form["openid.mode"] = "check_authentication";

        string body;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _http.PostAsync(SteamOpenIdUrl.LoginEndpoint, content, ct);
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AuthFailedException("Steam is unreachable or returned an error.", ex);
        }

        if (!IsValid(body))
            throw new AuthFailedException("Steam did not confirm the authentication.");
    }

    internal static bool IsValid(string responseBody) =>
        responseBody
            .Split('\n')
            .Any(line => line.Trim() == "is_valid:true");
}