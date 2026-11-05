using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SteamTracker.Domain.Models;
using SteamTracker.Integrations.Store;

namespace SteamTracker.Tests.Store;

public class StoreClientTests
{
    // Підміняє Store: відповідь будується за запитом, щоб тести не ходили в мережу
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<int, HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_respond(Requests.Count, request));
        }
    }

    private static (StoreClient Client, StubHandler Handler) Create(
        Func<int, HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        return (new StoreClient(new HttpClient(handler)), handler);
    }

    private static (StoreClient Client, StubHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Create((_, _) => Json(body, status));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static int[] AppIdsOf(Uri uri) =>
        Uri.UnescapeDataString(Regex.Match(uri.Query, "appids=([^&]*)").Groups[1].Value)
            .Split(',')
            .Select(int.Parse)
            .ToArray();

    // Кожен appid із запиту — платна гра за 99.99 UAH
    private static HttpResponseMessage PaidForEveryRequestedApp(HttpRequestMessage request)
    {
        const string paid =
            """{"success":true,"data":{"price_overview":{"currency":"UAH","initial":9999,"final":9999,"discount_percent":0}}}""";
        var entries = AppIdsOf(request.RequestUri!).Select(id => $"\"{id}\":{paid}");
        return Json("{" + string.Join(',', entries) + "}");
    }

    [Fact]
    public async Task GetPrices_120AppIds_SendsThreeBatches()
    {
        var (client, handler) = Create((_, request) => PaidForEveryRequestedApp(request));

        var prices = await client.GetPricesAsync(Enumerable.Range(1, 120));

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(new[] { 50, 50, 20 }, handler.Requests.Select(u => AppIdsOf(u).Length));
        Assert.All(handler.Requests, u => Assert.Contains("filters=price_overview", u.Query));
        Assert.Equal(120, prices.Count);
    }

    [Fact]
    public async Task GetPrices_TooManyRequests_ReturnsPartialResultWithoutThrowing()
    {
        var (client, handler) = Create((call, request) => call == 2
            ? Json("", HttpStatusCode.TooManyRequests)
            : PaidForEveryRequestedApp(request));

        var prices = await client.GetPricesAsync(Enumerable.Range(1, 120));

        // Після 429 третю пачку вже не запитуємо
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(Enumerable.Range(1, 50), prices.Select(p => p.AppId));
    }

    [Fact]
    public async Task GetPrices_PaidGame_MapsPrice()
    {
        var (client, _) = Create("""
            {"620":{"success":true,"data":{"price_overview":{"currency":"UAH","initial":19999,"final":4999,"discount_percent":75}}}}
            """);

        var prices = await client.GetPricesAsync(new[] { 620 });

        Assert.Equal(new GamePrice(620, 4999, "UAH", IsFree: false), Assert.Single(prices));
    }

    [Fact]
    public async Task GetPrices_FreeGame_IsFree()
    {
        // Для безкоштовної гри з filters=price_overview Store повертає "data": []
        var (client, _) = Create("""{"570":{"success":true,"data":[]}}""");

        var prices = await client.GetPricesAsync(new[] { 570 });

        Assert.Equal(new GamePrice(570, null, null, IsFree: true), Assert.Single(prices));
    }

    [Fact]
    public async Task GetPrices_UnavailableInRegion_NoPriceAndNotFree()
    {
        var (client, _) = Create("""{"12345":{"success":false}}""");

        var prices = await client.GetPricesAsync(new[] { 12345 });

        Assert.Equal(new GamePrice(12345, null, null, IsFree: false), Assert.Single(prices));
    }

    [Theory]
    [InlineData("not json", HttpStatusCode.OK)]
    [InlineData("", HttpStatusCode.InternalServerError)]
    public async Task GetPrices_BrokenResponse_ReturnsEmptyWithoutThrowing(string body, HttpStatusCode status)
    {
        var (client, _) = Create(body, status);

        var prices = await client.GetPricesAsync(new[] { 620 });

        Assert.Empty(prices);
    }

    [Fact]
    public async Task GetPrices_NetworkError_ReturnsEmptyWithoutThrowing()
    {
        var (client, _) = Create((_, _) => throw new HttpRequestException("no network"));

        var prices = await client.GetPricesAsync(new[] { 620 });

        Assert.Empty(prices);
    }

    [Fact]
    public async Task GetPrices_NoAppIds_SendsNoRequests()
    {
        var (client, handler) = Create((_, request) => PaidForEveryRequestedApp(request));

        var prices = await client.GetPricesAsync(Array.Empty<int>());

        Assert.Empty(prices);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPrices_DuplicateAppIds_RequestedOnce()
    {
        var (client, handler) = Create((_, request) => PaidForEveryRequestedApp(request));

        var prices = await client.GetPricesAsync(new[] { 620, 620, 570 });

        Assert.Equal(new[] { 620, 570 }, AppIdsOf(Assert.Single(handler.Requests)));
        Assert.Equal(2, prices.Count);
    }
}
