using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.PricePerHour;

namespace SteamTracker.Tests.PricePerHour;

public class PriceAnalyzerServiceTests
{
    private readonly Mock<ILibraryService> _library = new();
    private readonly Mock<IStoreClient> _store = new();

    // Які appid сервіс просив у Store при кожному виклику
    private readonly List<int[]> _storeRequests = new();

    private PriceAnalyzerService CreateService() => new(_library.Object, _store.Object);

    private static OwnedGame Game(int id, string name, int minutes) =>
        new(id, name, minutes, 0, null, "");

    private static GamePrice Paid(int id, int cents) => new(id, cents, "UAH", IsFree: false);

    private static GamePrice Free(int id) => new(id, null, null, IsFree: true);

    private void SetupLibrary(params OwnedGame[] games)
    {
        IReadOnlyList<OwnedGame> list = games;
        _library
            .Setup(l => l.GetMyLibraryAsync(It.IsAny<LibraryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<IReadOnlyList<OwnedGame>>(list, DataStatus.Available));
    }

    // Store повертає ціни лише для запитаних appid, яких є в prices
    private void SetupStore(params GamePrice[] prices)
    {
        _store
            .Setup(s => s.GetPricesAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<int> ids, CancellationToken _) =>
            {
                var requested = ids.ToArray();
                _storeRequests.Add(requested);
                return prices.Where(p => requested.Contains(p.AppId)).ToList();
            });
    }

    private static PricePerHourRow Row(PriceAnalysis analysis, int appId) =>
        analysis.Games.Single(r => r.AppId == appId);

    [Fact]
    public async Task GetAnalysisAsync_PaidGame_PricePerHourIsPriceDividedByHours()
    {
        SetupLibrary(Game(1, "A", minutes: 120));
        SetupStore(Paid(1, 1000));

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.Null(result.Message);
        var row = Row(result.Value!, 1);
        Assert.Equal(500m, row.PricePerHourCents);
        Assert.Equal(1000, row.PriceCents);
        Assert.Equal("UAH", row.Currency);
        Assert.False(row.PriceUnavailable);
    }

    [Fact]
    public async Task GetAnalysisAsync_ZeroMinutes_NoDivisionAndNotInBestValue()
    {
        SetupLibrary(Game(1, "NeverPlayed", minutes: 0));
        SetupStore(Paid(1, 1000));

        var result = await CreateService().GetAnalysisAsync();

        var row = Row(result.Value!, 1);
        Assert.Null(row.PricePerHourCents);
        Assert.False(row.PriceUnavailable);
        Assert.Empty(result.Value!.BestValue);
    }

    [Fact]
    public async Task GetAnalysisAsync_FreeGames_ExcludedFromTopsAndMedian()
    {
        SetupLibrary(
            Game(1, "Free", minutes: 0),
            Game(2, "FreeLongPlayed", minutes: 6000),
            Game(3, "Paid", minutes: 30));
        SetupStore(Free(1), Free(2), Paid(3, 1000));

        var result = await CreateService().GetAnalysisAsync();

        var free = Row(result.Value!, 1);
        Assert.True(free.IsFree);
        Assert.False(free.PriceUnavailable);
        Assert.Null(free.PricePerHourCents);
        Assert.DoesNotContain(result.Value!.BestValue, r => r.IsFree);
        // Якби безкоштовні рахувались як 0 у медіані, платна гра потрапила б у топ
        Assert.Empty(result.Value!.ExpensiveBarelyPlayed);
    }

    [Fact]
    public async Task GetAnalysisAsync_FixedSet_BuildsBothTops()
    {
        SetupLibrary(
            Game(1, "A", minutes: 600),  // 1000 / 10 год = 100 за год
            Game(2, "B", minutes: 120),  // 2000 / 2 год = 1000 за год
            Game(3, "C", minutes: 60),   // 500 / 1 год = 500 за год
            Game(4, "D", minutes: 59),   // менше години — не в "найвигідніших"
            Game(5, "E", minutes: 0),
            Game(6, "F", minutes: 30),
            Game(7, "G", minutes: 0));
        SetupStore(Paid(1, 1000), Paid(2, 2000), Paid(3, 500), Paid(4, 100), Paid(5, 5000), Paid(6, 4000), Free(7));

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(new[] { "A", "C", "B" }, result.Value!.BestValue.Select(r => r.Name));
        // Медіана платних = (1000 + 2000) / 2 = 1500; B дорожча, але має рівно 2 год — не "майже не грав"
        Assert.Equal(new[] { "E", "F" }, result.Value!.ExpensiveBarelyPlayed.Select(r => r.Name));
    }

    [Fact]
    public async Task GetAnalysisAsync_TopsLimitedToTopSize()
    {
        var games = Enumerable.Range(1, PriceAnalyzerService.TopSize + 3)
            .Select(i => Game(i, $"G{i}", minutes: 60 * i))
            .ToArray();
        SetupLibrary(games);
        SetupStore(games.Select(g => Paid(g.AppId, 1000)).ToArray());

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(PriceAnalyzerService.TopSize, result.Value!.BestValue.Count);
    }

    [Fact]
    public async Task GetAnalysisAsync_StorePartial_RestMarkedUnavailableWithMessage()
    {
        SetupLibrary(Game(1, "WithPrice", minutes: 60), Game(2, "NoPrice", minutes: 60));
        SetupStore(Paid(1, 1000));

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.NotNull(result.Message);
        Assert.False(Row(result.Value!, 1).PriceUnavailable);
        var noPrice = Row(result.Value!, 2);
        Assert.True(noPrice.PriceUnavailable);
        Assert.Null(noPrice.PriceCents);
        Assert.Null(noPrice.PricePerHourCents);
        Assert.Equal(60, noPrice.PlaytimeMinutes);
    }

    [Fact]
    public async Task GetAnalysisAsync_StoreUnavailable_AvailableWithPlaytimeOnly()
    {
        SetupLibrary(Game(1, "A", minutes: 60), Game(2, "B", minutes: 0));
        SetupStore();

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(DataStatus.Available, result.Status);
        Assert.NotNull(result.Message);
        Assert.All(result.Value!.Games, r => Assert.True(r.PriceUnavailable));
        Assert.Empty(result.Value!.BestValue);
        Assert.Empty(result.Value!.ExpensiveBarelyPlayed);
    }

    [Fact]
    public async Task GetAnalysisAsync_NotSoldInRegion_UnavailableWithoutMessage()
    {
        SetupLibrary(Game(1, "A", minutes: 60));
        SetupStore(new GamePrice(1, null, null, IsFree: false));

        var result = await CreateService().GetAnalysisAsync();

        Assert.True(Row(result.Value!, 1).PriceUnavailable);
        // Store відповів — збою не було, банер не потрібен
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task GetAnalysisAsync_PriceInCache_StoreNotCalledAgain()
    {
        SetupLibrary(Game(1, "A", minutes: 60), Game(2, "B", minutes: 60));
        SetupStore(Paid(1, 1000), Paid(2, 2000));
        var service = CreateService();

        await service.GetAnalysisAsync();
        var second = await service.GetAnalysisAsync();

        Assert.Single(_storeRequests);
        Assert.Equal(2000, Row(second.Value!, 2).PriceCents);
    }

    [Fact]
    public async Task GetAnalysisAsync_SecondCall_RequestsOnlyMissingPrices()
    {
        SetupLibrary(Game(1, "A", minutes: 60), Game(2, "B", minutes: 60));
        SetupStore(Paid(1, 1000)); // ціну гри 2 Store не віддав
        var service = CreateService();

        await service.GetAnalysisAsync();
        await service.GetAnalysisAsync();

        Assert.Equal(2, _storeRequests.Count);
        Assert.Equal(new[] { 2 }, _storeRequests[1]);
    }

    [Theory]
    [InlineData(DataStatus.Private)]
    [InlineData(DataStatus.Error)]
    public async Task GetAnalysisAsync_LibraryNotAvailable_ReturnsSameStatusWithoutStore(DataStatus status)
    {
        _library
            .Setup(l => l.GetMyLibraryAsync(It.IsAny<LibraryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<IReadOnlyList<OwnedGame>>(null, status, "msg"));
        SetupStore();

        var result = await CreateService().GetAnalysisAsync();

        Assert.Equal(status, result.Status);
        Assert.Equal("msg", result.Message);
        Assert.Null(result.Value);
        Assert.Empty(_storeRequests);
    }

    [Fact]
    public async Task GetAnalysisAsync_Always_HasCurrentPriceNote()
    {
        SetupLibrary(Game(1, "A", minutes: 60));
        SetupStore(Paid(1, 1000));

        var result = await CreateService().GetAnalysisAsync();

        Assert.False(string.IsNullOrWhiteSpace(result.Value!.Note));
    }
}
