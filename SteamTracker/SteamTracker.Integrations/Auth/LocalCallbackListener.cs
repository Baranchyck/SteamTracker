using System.Net;
using System.Text;

namespace SteamTracker.Integrations.Auth;

internal sealed class LocalCallbackListener : IDisposable
{
    private const string ClosePageHtml =
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>SteamTracker</title></head>" +
        "<body style=\"font-family:sans-serif;text-align:center;margin-top:20vh\">" +
        "<h2>Готово. Можна закрити вкладку.</h2></body></html>";

    private readonly HttpListener _listener = new();

    public LocalCallbackListener(int port)
    {
        _listener.Prefixes.Add($"http://localhost:{port}/callback/");
    }

    public void Start() => _listener.Start();

    public async Task<IReadOnlyDictionary<string, string>> WaitForCallbackAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var registration = ct.Register(_listener.Stop);

        HttpListenerContext context;
        try
        {
            context = await _listener.GetContextAsync();
        }
        catch (Exception ex) when (ex is HttpListenerException
                                      or ObjectDisposedException
                                      or InvalidOperationException)
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }

        var query = context.Request.QueryString;
        var parameters = new Dictionary<string, string>();
        foreach (var key in query.AllKeys)
        {
            if (key is null) continue;
            parameters[key] = query[key] ?? string.Empty;
        }

        await RespondAsync(context.Response);
        return parameters;
    }

    private static async Task RespondAsync(HttpListenerResponse response)
    {
        var bytes = Encoding.UTF8.GetBytes(ClosePageHtml);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose() => _listener.Close();
}