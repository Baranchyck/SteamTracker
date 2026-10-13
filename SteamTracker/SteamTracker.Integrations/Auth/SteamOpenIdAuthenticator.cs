using System.Net;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Abstractions;
namespace SteamTracker.Integrations.Auth;

public sealed class SteamOpenIdAuthenticator : ISteamOpenIdAuthenticator
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(3);

    private readonly HttpClient _http;

    public SteamOpenIdAuthenticator(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> AuthenticateAsync(CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(LoginTimeout);

        try
        {
            var port = LocalPort.FindFree();
            var returnTo = $"http://localhost:{port}/callback";
            var realm = $"http://localhost:{port}/";

            using var listener = new LocalCallbackListener(port);
            listener.Start();

            BrowserLauncher.Open(SteamOpenIdUrl.Build(returnTo, realm));

            var parameters = await listener.WaitForCallbackAsync(timeoutCts.Token);

            var steamId = SteamOpenIdValidator.ExtractSteamId(parameters, returnTo);
            await new SteamOpenIdVerifier(_http).VerifyAsync(parameters, timeoutCts.Token);

            return steamId;
        }
        catch (OperationCanceledException ex)
        {
            throw new AuthCancelledException("Authentication was cancelled or timed out.", ex);
        }
        catch (HttpListenerException ex)
        {
            throw new AuthFailedException("Could not start the local callback listener.", ex);
        }
    }
}