using System.Net;
using System.Text;

namespace SteamTracker.Tests.SteamApi;

// На кожен запит відповідає функцією й запам'ятовує всі запити (кількість, URL, пачки)
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _respond;

    public RecordingHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;

    public List<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        var (status, body) = _respond(request);
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }
}

// Імітує мережеву помилку / таймаут ще до відповіді
internal sealed class ThrowingHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromException<HttpResponseMessage>(_exception);
}
