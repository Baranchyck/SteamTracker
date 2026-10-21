using Microsoft.Extensions.Configuration;
using SteamTracker.Domain.Exceptions;
using SteamTracker.Services.Configuration;

namespace SteamTracker.Tests.Configuration;

public class SteamOptionsTests
{
    private static IConfiguration BuildConfiguration(string? apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Steam:ApiKey"] = apiKey })
            .Build();

    [Fact]
    public void Load_KeyPresent_ReturnsOptions()
    {
        var options = SteamOptions.Load(BuildConfiguration("ABC123"));

        Assert.Equal("ABC123", options.ApiKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(SteamOptions.ApiKeyPlaceholder)]
    public void Load_KeyMissingOrPlaceholder_Throws(string? apiKey)
    {
        var ex = Assert.Throws<ConfigurationMissingException>(() =>
            SteamOptions.Load(BuildConfiguration(apiKey)));

        Assert.Contains("Steam:ApiKey", ex.Message);
    }

    [Fact]
    public void Load_SectionMissing_Throws()
    {
        var empty = new ConfigurationBuilder().Build();

        Assert.Throws<ConfigurationMissingException>(() => SteamOptions.Load(empty));
    }
}
