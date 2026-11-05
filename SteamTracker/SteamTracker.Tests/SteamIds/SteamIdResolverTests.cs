using Moq;
using SteamTracker.Domain.Abstractions;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Domain.Models;
using SteamTracker.Services.SteamIds;

namespace SteamTracker.Tests.SteamIds;

public class SteamIdResolverTests
{
    private const string ValidId = "76561197960287930";
    private const string VanityId = "76561198000000042";

    private readonly Mock<ISteamApiClient> _steamApi = new();

    public SteamIdResolverTests()
    {
        // Відомий нік → VanityId, будь-який інший → не знайдено
        _steamApi.Setup(s => s.ResolveVanityUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _steamApi.Setup(s => s.ResolveVanityUrlAsync("gabelogannewell", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VanityId);
    }

    private SteamIdResolver CreateResolver() => new(_steamApi.Object);

    [Theory]
    [InlineData(ValidId, ValidId)]
    [InlineData("  " + ValidId + "  ", ValidId)]
    [InlineData("https://steamcommunity.com/profiles/" + ValidId, ValidId)]
    [InlineData("http://steamcommunity.com/profiles/" + ValidId + "/", ValidId)]
    [InlineData("https://www.steamcommunity.com/profiles/" + ValidId + "/", ValidId)]
    [InlineData("steamcommunity.com/profiles/" + ValidId, ValidId)]
    [InlineData("HTTPS://SteamCommunity.com/Profiles/" + ValidId + "?l=ukrainian", ValidId)]
    [InlineData("https://steamcommunity.com/id/gabelogannewell", VanityId)]
    [InlineData("https://steamcommunity.com/id/gabelogannewell/", VanityId)]
    [InlineData("steamcommunity.com/id/gabelogannewell#comments", VanityId)]
    [InlineData("gabelogannewell", VanityId)]
    [InlineData(" gabelogannewell ", VanityId)]
    public async Task ResolveAsync_ValidInput_ReturnsSteamId(string input, string expected)
    {
        var result = await CreateResolver().ResolveAsync(input);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expected, result.SteamId);
        Assert.Null(result.Error);
        Assert.Equal(input, result.Input);
    }

    [Theory]
    [InlineData("", SteamIdResolveError.Empty)]
    [InlineData("   ", SteamIdResolveError.Empty)]
    [InlineData("12345678901234567", SteamIdResolveError.InvalidSteamId)]      // 17 цифр, не той префікс
    [InlineData("7656119796028793", SteamIdResolveError.InvalidSteamId)]       // 16 цифр
    [InlineData("765611979602879301", SteamIdResolveError.InvalidSteamId)]     // 18 цифр
    [InlineData("https://steamcommunity.com/profiles/123", SteamIdResolveError.InvalidSteamId)]
    [InlineData("https://evil.com/profiles/" + ValidId, SteamIdResolveError.ForeignHost)]
    [InlineData("https://steamcommunity.com.evil.com/id/x", SteamIdResolveError.ForeignHost)]
    [InlineData("https://store.steampowered.com/app/570", SteamIdResolveError.ForeignHost)]
    [InlineData("ftp://steamcommunity.com/id/abc", SteamIdResolveError.InvalidFormat)]
    [InlineData("https://steamcommunity.com", SteamIdResolveError.InvalidFormat)]
    [InlineData("https://steamcommunity.com/groups/valve", SteamIdResolveError.InvalidFormat)]
    [InlineData("https://steamcommunity.com/id/a/b", SteamIdResolveError.InvalidFormat)]
    [InlineData("нік кирилицею", SteamIdResolveError.InvalidFormat)]
    [InlineData("a&key=x", SteamIdResolveError.InvalidFormat)]
    public async Task ResolveAsync_InvalidInput_ReturnsErrorWithoutApiCall(string input, SteamIdResolveError expected)
    {
        var result = await CreateResolver().ResolveAsync(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
        Assert.False(string.IsNullOrEmpty(result.Message));
        _steamApi.Verify(s => s.ResolveVanityUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_TooLong_ReturnsTooLong()
    {
        var input = new string('a', SteamIdResolver.MaxInputLength + 1);

        var result = await CreateResolver().ResolveAsync(input);

        Assert.Equal(SteamIdResolveError.TooLong, result.Error);
    }

    [Fact]
    public async Task ResolveAsync_UnknownVanity_ReturnsNotFound()
    {
        var result = await CreateResolver().ResolveAsync("nosuchuser");

        Assert.Equal(SteamIdResolveError.NotFound, result.Error);
        Assert.Contains("nosuchuser", result.Message);
    }

    [Fact]
    public async Task ResolveAsync_SteamFails_ReturnsSteamUnavailable()
    {
        _steamApi.Setup(s => s.ResolveVanityUrlAsync("someone", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SteamApiException("boom"));

        var result = await CreateResolver().ResolveAsync("someone");

        Assert.Equal(SteamIdResolveError.SteamUnavailable, result.Error);
    }

    [Fact]
    public async Task ResolveAsync_SteamReturnsGarbageId_NotTrusted()
    {
        _steamApi.Setup(s => s.ResolveVanityUrlAsync("weird", It.IsAny<CancellationToken>()))
            .ReturnsAsync("123");

        var result = await CreateResolver().ResolveAsync("weird");

        Assert.False(result.IsSuccess);
    }
}
